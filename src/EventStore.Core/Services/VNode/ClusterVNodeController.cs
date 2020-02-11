using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Cluster;
using EventStore.Core.Cluster.Settings;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Services.UserManagement;
using EventStore.Core.TransactionLog.Chunks;

namespace EventStore.Core.Services.VNode {
	public class ClusterVNodeController : IHandle<Message> {
		public static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(5);
		public static readonly TimeSpan LeaderReconnectionDelay = TimeSpan.FromMilliseconds(500);
		private static readonly TimeSpan LeaderSubscriptionRetryDelay = TimeSpan.FromMilliseconds(500);
		private static readonly TimeSpan LeaderSubscriptionTimeout = TimeSpan.FromMilliseconds(1000);

		private static readonly ILogger Log = LogManager.GetLoggerFor<ClusterVNodeController>();

		private readonly IPublisher _outputBus;
		private readonly VNodeInfo _nodeInfo;
		private readonly TFChunkDb _db;
		private readonly ClusterVNode _node;

		private VNodeState _state = VNodeState.Initializing;
		private VNodeInfo _leader;
		private Guid _stateCorrelationId = Guid.NewGuid();
		private Guid _subscriptionId = Guid.Empty;

		private IQueuedHandler _mainQueue;
		private IEnvelope _publishEnvelope;
		private readonly VNodeFSM _fsm;

		private readonly MessageForwardingProxy _forwardingProxy;
		private readonly TimeSpan _forwardingTimeout;
		private readonly ISubsystem[] _subSystems;

		private int _subSystemInitsToExpect;

		private int _serviceInitsToExpect = 1 /* StorageChaser */
		                                    + 1 /* StorageReader */
		                                    + 1 /* StorageWriter */;

		private int _serviceShutdownsToExpect = 1 /* StorageChaser */
		                                        + 1 /* StorageReader */
		                                        + 1 /* StorageWriter */
		                                        + 1 /* IndexCommitterService */
		                                        + 1 /* LeaderReplicationService */
		                                        + 1 /* HttpService Internal*/
		                                        + 1 /* HttpService External*/;

		private bool _exitProcessOnShutdown;

		public ClusterVNodeController(IPublisher outputBus, VNodeInfo nodeInfo, TFChunkDb db,
			ClusterVNodeSettings vnodeSettings, ClusterVNode node,
			MessageForwardingProxy forwardingProxy, ISubsystem[] subSystems) {
			Ensure.NotNull(outputBus, "outputBus");
			Ensure.NotNull(nodeInfo, "nodeInfo");
			Ensure.NotNull(db, "dbConfig");
			Ensure.NotNull(vnodeSettings, "vnodeSettings");
			Ensure.NotNull(node, "node");
			Ensure.NotNull(forwardingProxy, "forwardingProxy");

			_outputBus = outputBus;
			_nodeInfo = nodeInfo;
			_db = db;
			_node = node;
			_subSystems = subSystems;
			if (vnodeSettings.ClusterNodeCount == 1) {
				_serviceShutdownsToExpect = 1 /* StorageChaser */
				                            + 1 /* StorageReader */
				                            + 1 /* StorageWriter */
				                            + 1 /* IndexCommitterService */
				                            + 1 /* HttpService External*/;
			}

			_subSystemInitsToExpect = _subSystems != null ? subSystems.Length : 0;

			_forwardingProxy = forwardingProxy;
			_forwardingTimeout = vnodeSettings.PrepareTimeout + vnodeSettings.CommitTimeout +
			                     TimeSpan.FromMilliseconds(300);

			_fsm = CreateFSM();
		}

		public void SetMainQueue(IQueuedHandler mainQueue) {
			Ensure.NotNull(mainQueue, "mainQueue");

			_mainQueue = mainQueue;
			_publishEnvelope = new PublishEnvelope(mainQueue);
		}

		private VNodeFSM CreateFSM() {
			var stm = new VNodeFSMBuilder(() => _state)
				.InAnyState()
				.When<SystemMessage.StateChangeMessage>()
				.Do(m => Application.Exit(ExitCode.Error,
					string.Format("{0} message was unhandled in {1}.", m.GetType().Name, GetType().Name)))
				.When<UserManagementMessage.UserManagementServiceInitialized>().Do(Handle)
				.When<SystemMessage.SubSystemInitialized>().Do(Handle)
				.When<SystemMessage.SystemCoreReady>().Do(Handle)
				.InState(VNodeState.Initializing)
				.When<SystemMessage.SystemInit>().Do(Handle)
				.When<SystemMessage.SystemStart>().Do(Handle)
				.When<SystemMessage.ServiceInitialized>().Do(Handle)
				.When<SystemMessage.BecomeReadOnlyLeaderless>().Do(Handle)
				.When<ClientMessage.ScavengeDatabase>().Ignore()
				.When<ClientMessage.StopDatabaseScavenge>().Ignore()
				.WhenOther().ForwardTo(_outputBus)
				.InStates(VNodeState.Unknown, VNodeState.ReadOnlyLeaderless)
				.WhenOther().ForwardTo(_outputBus)
				.InStates(VNodeState.Initializing, VNodeState.Leader, VNodeState.ResigningLeader, VNodeState.PreLeader,
					VNodeState.PreReplica, VNodeState.CatchingUp, VNodeState.Clone, VNodeState.Slave)
				.When<SystemMessage.BecomeUnknown>().Do(Handle)
				.InAllStatesExcept(VNodeState.Unknown,
					VNodeState.PreReplica, VNodeState.CatchingUp, VNodeState.Clone, VNodeState.Slave,
					VNodeState.Leader, VNodeState.ResigningLeader, VNodeState.ReadOnlyLeaderless,
					VNodeState.PreReadOnlyReplica, VNodeState.ReadOnlyReplica)
				.When<ClientMessage.ReadRequestMessage>()
				.Do(msg => DenyRequestBecauseNotReady(msg.Envelope, msg.CorrelationId))
				.InAllStatesExcept(VNodeState.Leader, VNodeState.ResigningLeader,
					VNodeState.PreReplica, VNodeState.CatchingUp, VNodeState.Clone, VNodeState.Slave,
					VNodeState.ReadOnlyReplica, VNodeState.PreReadOnlyReplica)
				.When<ClientMessage.WriteRequestMessage>()
				.Do(msg => DenyRequestBecauseNotReady(msg.Envelope, msg.CorrelationId))
				.InState(VNodeState.Leader)
				.When<ClientMessage.ReadEvent>().ForwardTo(_outputBus)
				.When<ClientMessage.ReadStreamEventsForward>().ForwardTo(_outputBus)
				.When<ClientMessage.ReadStreamEventsBackward>().ForwardTo(_outputBus)
				.When<ClientMessage.ReadAllEventsForward>().ForwardTo(_outputBus)
				.When<ClientMessage.ReadAllEventsBackward>().ForwardTo(_outputBus)
				.When<ClientMessage.FilteredReadAllEventsForward>().ForwardTo(_outputBus)
				.When<ClientMessage.FilteredReadAllEventsBackward>().ForwardTo(_outputBus)
				.When<ClientMessage.WriteEvents>().ForwardTo(_outputBus)
				.When<ClientMessage.TransactionStart>().ForwardTo(_outputBus)
				.When<ClientMessage.TransactionWrite>().ForwardTo(_outputBus)
				.When<ClientMessage.TransactionCommit>().ForwardTo(_outputBus)
				.When<ClientMessage.DeleteStream>().ForwardTo(_outputBus)
				.When<ClientMessage.CreatePersistentSubscription>().ForwardTo(_outputBus)
				.When<ClientMessage.ConnectToPersistentSubscription>().ForwardTo(_outputBus)
				.When<ClientMessage.UpdatePersistentSubscription>().ForwardTo(_outputBus)
				.When<ClientMessage.DeletePersistentSubscription>().ForwardTo(_outputBus)
				.When<SystemMessage.InitiateLeaderResignation>().Do(Handle)
				.When<SystemMessage.BecomeResigningLeader>().Do(Handle)
				.InState(VNodeState.ResigningLeader)
				.When<ClientMessage.ReadEvent>().ForwardTo(_outputBus)
				.When<ClientMessage.ReadStreamEventsForward>().ForwardTo(_outputBus)
				.When<ClientMessage.ReadStreamEventsBackward>().ForwardTo(_outputBus)
				.When<ClientMessage.ReadAllEventsForward>().ForwardTo(_outputBus)
				.When<ClientMessage.ReadAllEventsBackward>().ForwardTo(_outputBus)
				.When<ClientMessage.WriteEvents>().Ignore()
				.When<ClientMessage.TransactionStart>().Ignore()
				.When<ClientMessage.TransactionWrite>().Ignore()
				.When<ClientMessage.TransactionCommit>().Ignore()
				.When<ClientMessage.DeleteStream>().Ignore()
				.When<ClientMessage.CreatePersistentSubscription>().Ignore()
				.When<ClientMessage.ConnectToPersistentSubscription>().Ignore()
				.When<ClientMessage.UpdatePersistentSubscription>().Ignore()
				.When<ClientMessage.DeletePersistentSubscription>().Ignore()
				.When<SystemMessage.RequestQueueDrained>().Do(Handle)
				.InAllStatesExcept(VNodeState.ResigningLeader)
				.When<SystemMessage.RequestQueueDrained>().Ignore()
				.InStates(VNodeState.PreReplica, VNodeState.CatchingUp, VNodeState.Clone, VNodeState.Slave,
					VNodeState.Unknown, VNodeState.ReadOnlyLeaderless,
					VNodeState.PreReadOnlyReplica, VNodeState.ReadOnlyReplica)
				.When<ClientMessage.ReadEvent>().Do(HandleAsNonLeader)
				.When<ClientMessage.ReadStreamEventsForward>().Do(HandleAsNonLeader)
				.When<ClientMessage.ReadStreamEventsBackward>().Do(HandleAsNonLeader)
				.When<ClientMessage.ReadAllEventsForward>().Do(HandleAsNonLeader)
				.When<ClientMessage.ReadAllEventsBackward>().Do(HandleAsNonLeader)
				.When<ClientMessage.FilteredReadAllEventsForward>().Do(HandleAsNonLeader)
				.When<ClientMessage.FilteredReadAllEventsBackward>().Do(HandleAsNonLeader)
				.When<ClientMessage.CreatePersistentSubscription>().Do(HandleAsNonLeader)
				.When<ClientMessage.ConnectToPersistentSubscription>().Do(HandleAsNonLeader)
				.When<ClientMessage.UpdatePersistentSubscription>().Do(HandleAsNonLeader)
				.When<ClientMessage.DeletePersistentSubscription>().Do(HandleAsNonLeader)
				.InStates(VNodeState.ReadOnlyLeaderless, VNodeState.PreReadOnlyReplica, VNodeState.ReadOnlyReplica)
				.When<ClientMessage.WriteEvents>().Do(HandleAsReadOnlyReplica)
				.When<ClientMessage.TransactionStart>().Do(HandleAsReadOnlyReplica)
				.When<ClientMessage.TransactionWrite>().Do(HandleAsReadOnlyReplica)
				.When<ClientMessage.TransactionCommit>().Do(HandleAsReadOnlyReplica)
				.When<ClientMessage.DeleteStream>().Do(HandleAsReadOnlyReplica)
				.When<SystemMessage.VNodeConnectionLost>().Do(HandleAsReadOnlyReplica)
				.When<ElectionMessage.ElectionsDone>().Do(HandleAsReadOnlyReplica)
				.When<SystemMessage.BecomePreReadOnlyReplica>().Do(Handle)
				.InStates(VNodeState.PreReplica, VNodeState.CatchingUp, VNodeState.Clone,VNodeState.Slave)
				.When<ClientMessage.WriteEvents>().Do(HandleAsNonLeader)
				.When<ClientMessage.TransactionStart>().Do(HandleAsNonLeader)
				.When<ClientMessage.TransactionWrite>().Do(HandleAsNonLeader)
				.When<ClientMessage.TransactionCommit>().Do(HandleAsNonLeader)
				.When<ClientMessage.DeleteStream>().Do(HandleAsNonLeader)
				.InAnyState()
				.When<ClientMessage.NotHandled>().ForwardTo(_outputBus)
				.When<ClientMessage.ReadEventCompleted>().ForwardTo(_outputBus)
				.When<ClientMessage.ReadStreamEventsForwardCompleted>().ForwardTo(_outputBus)
				.When<ClientMessage.ReadStreamEventsBackwardCompleted>().ForwardTo(_outputBus)
				.When<ClientMessage.ReadAllEventsForwardCompleted>().ForwardTo(_outputBus)
				.When<ClientMessage.ReadAllEventsBackwardCompleted>().ForwardTo(_outputBus)
				.When<ClientMessage.FilteredReadAllEventsForwardCompleted>().ForwardTo(_outputBus)
				.When<ClientMessage.FilteredReadAllEventsBackwardCompleted>().ForwardTo(_outputBus)
				.When<ClientMessage.WriteEventsCompleted>().ForwardTo(_outputBus)
				.When<ClientMessage.TransactionStartCompleted>().ForwardTo(_outputBus)
				.When<ClientMessage.TransactionWriteCompleted>().ForwardTo(_outputBus)
				.When<ClientMessage.TransactionCommitCompleted>().ForwardTo(_outputBus)
				.When<ClientMessage.DeleteStreamCompleted>().ForwardTo(_outputBus)
				.InAllStatesExcept(VNodeState.Initializing, VNodeState.ShuttingDown, VNodeState.Shutdown,
				VNodeState.ReadOnlyLeaderless, VNodeState.PreReadOnlyReplica, VNodeState.ReadOnlyReplica)
				.When<ElectionMessage.ElectionsDone>().Do(Handle)
				.InStates(VNodeState.Unknown,
					VNodeState.PreReplica, VNodeState.CatchingUp, VNodeState.Clone, VNodeState.Slave,
					VNodeState.PreLeader, VNodeState.Leader)
				.When<SystemMessage.BecomePreReplica>().Do(Handle)
				.When<SystemMessage.BecomePreLeader>().Do(Handle)
				.InStates(VNodeState.PreReplica, VNodeState.CatchingUp, VNodeState.Clone, VNodeState.Slave)
				.When<GossipMessage.GossipUpdated>().Do(HandleAsNonLeader)
				.When<SystemMessage.VNodeConnectionLost>().Do(Handle)
				.InAllStatesExcept(VNodeState.PreReplica, VNodeState.PreLeader, VNodeState.PreReadOnlyReplica)
				.When<SystemMessage.WaitForChaserToCatchUp>().Ignore()
				.When<SystemMessage.ChaserCaughtUp>().Ignore()
				.InStates(VNodeState.PreReplica, VNodeState.PreReadOnlyReplica)
				.When<SystemMessage.BecomeCatchingUp>().Do(Handle)
				.When<SystemMessage.WaitForChaserToCatchUp>().Do(Handle)
				.When<SystemMessage.ChaserCaughtUp>().Do(HandleAsPreReplica)
				.When<ReplicationMessage.ReconnectToLeader>().Do(Handle)
				.When<ReplicationMessage.SubscribeToLeader>().Do(Handle)
				.When<ReplicationMessage.ReplicaSubscriptionRetry>().Do(Handle)
				.When<ReplicationMessage.ReplicaSubscribed>().Do(Handle)
				.WhenOther().ForwardTo(_outputBus)
				.InAllStatesExcept(VNodeState.PreReplica, VNodeState.PreReadOnlyReplica)
				.When<ReplicationMessage.ReconnectToLeader>().Ignore()
				.When<ReplicationMessage.SubscribeToLeader>().Ignore()
				.When<ReplicationMessage.ReplicaSubscriptionRetry>().Ignore()
				.When<ReplicationMessage.ReplicaSubscribed>().Ignore()
				.InStates(VNodeState.CatchingUp, VNodeState.Clone, VNodeState.Slave, VNodeState.ReadOnlyReplica)
				.When<ReplicationMessage.CreateChunk>().Do(ForwardReplicationMessage)
				.When<ReplicationMessage.RawChunkBulk>().Do(ForwardReplicationMessage)
				.When<ReplicationMessage.DataChunkBulk>().Do(ForwardReplicationMessage)
				.When<ReplicationMessage.AckLogPosition>().ForwardTo(_outputBus)
				.WhenOther().ForwardTo(_outputBus)
				.InAllStatesExcept(VNodeState.CatchingUp, VNodeState.Clone, VNodeState.Slave, VNodeState.ReadOnlyReplica)
				.When<ReplicationMessage.CreateChunk>().Ignore()
				.When<ReplicationMessage.RawChunkBulk>().Ignore()
				.When<ReplicationMessage.DataChunkBulk>().Ignore()
				.When<ReplicationMessage.AckLogPosition>().Ignore()
				.InState(VNodeState.CatchingUp)
				.When<ReplicationMessage.CloneAssignment>().Do(Handle)
				.When<ReplicationMessage.SlaveAssignment>().Do(Handle)
				.When<SystemMessage.BecomeClone>().Do(Handle)
				.When<SystemMessage.BecomeSlave>().Do(Handle)
				.InState(VNodeState.Clone)
				.When<ReplicationMessage.DropSubscription>().Do(Handle)
				.When<ReplicationMessage.SlaveAssignment>().Do(Handle)
				.When<SystemMessage.BecomeSlave>().Do(Handle)
				.InState(VNodeState.Slave)
				.When<ReplicationMessage.CloneAssignment>().Do(Handle)
				.When<SystemMessage.BecomeClone>().Do(Handle)
				.InStates(VNodeState.PreReadOnlyReplica, VNodeState.ReadOnlyReplica)
				.When<GossipMessage.GossipUpdated>().Do(HandleAsReadOnlyReplica)
				.When<SystemMessage.BecomeReadOnlyLeaderless>().Do(Handle)
				.InState(VNodeState.PreReadOnlyReplica)
				.When<SystemMessage.BecomeReadOnlyReplica>().Do(Handle)
				.InStates(VNodeState.PreLeader, VNodeState.Leader, VNodeState.ResigningLeader)
				.When<GossipMessage.GossipUpdated>().Do(HandleAsLeader)
				.When<ReplicationMessage.ReplicaSubscriptionRequest>().ForwardTo(_outputBus)
				.When<ReplicationMessage.ReplicaLogPositionAck>().ForwardTo(_outputBus)
				.InAllStatesExcept(VNodeState.PreLeader, VNodeState.Leader, VNodeState.ResigningLeader)
				.When<ReplicationMessage.ReplicaSubscriptionRequest>().Ignore()
				.InState(VNodeState.PreLeader)
				.When<SystemMessage.BecomeLeader>().Do(Handle)
				.When<SystemMessage.WaitForChaserToCatchUp>().Do(Handle)
				.When<SystemMessage.ChaserCaughtUp>().Do(HandleAsPreLeader)
				.WhenOther().ForwardTo(_outputBus)
				.InStates(VNodeState.Leader, VNodeState.ResigningLeader)
				.When<SystemMessage.NoQuorumMessage>().Do(Handle)
				.When<StorageMessage.WritePrepares>().ForwardTo(_outputBus)
				.When<StorageMessage.WriteDelete>().ForwardTo(_outputBus)
				.When<StorageMessage.WriteTransactionStart>().ForwardTo(_outputBus)
				.When<StorageMessage.WriteTransactionData>().ForwardTo(_outputBus)
				.When<StorageMessage.WriteTransactionEnd>().ForwardTo(_outputBus)
				.When<StorageMessage.WriteCommit>().ForwardTo(_outputBus)
				.WhenOther().ForwardTo(_outputBus)
				.InAllStatesExcept(VNodeState.Leader, VNodeState.ResigningLeader)
				.When<SystemMessage.NoQuorumMessage>().Ignore()
				.When<SystemMessage.InitiateLeaderResignation>().Ignore()
				.When<SystemMessage.BecomeResigningLeader>().Ignore()
				.When<StorageMessage.WritePrepares>().Ignore()
				.When<StorageMessage.WriteDelete>().Ignore()
				.When<StorageMessage.WriteTransactionStart>().Ignore()
				.When<StorageMessage.WriteTransactionData>().Ignore()
				.When<StorageMessage.WriteTransactionEnd>().Ignore()
				.When<StorageMessage.WriteCommit>().Ignore()
				.InAllStatesExcept(VNodeState.ShuttingDown, VNodeState.Shutdown)
				.When<ClientMessage.RequestShutdown>().Do(Handle)
				.When<SystemMessage.BecomeShuttingDown>().Do(Handle)
				.InState(VNodeState.ShuttingDown)
				.When<SystemMessage.BecomeShutdown>().Do(Handle)
				.When<SystemMessage.ShutdownTimeout>().Do(Handle)
				.InStates(VNodeState.ShuttingDown, VNodeState.Shutdown)
				.When<SystemMessage.ServiceShutdown>().Do(Handle)
				.WhenOther().ForwardTo(_outputBus)
				.Build();
			return stm;
		}

		void IHandle<Message>.Handle(Message message) {
			_fsm.Handle(message);
		}

		private void Handle(SystemMessage.SystemInit message) {
			Log.Info("========== [{internalHttp}] SYSTEM INIT...", _nodeInfo.InternalHttp);
			_outputBus.Publish(message);
		}

		private void Handle(SystemMessage.SystemStart message) {
			Log.Info("========== [{internalHttp}] SYSTEM START...", _nodeInfo.InternalHttp);
			_outputBus.Publish(message);
			if (_nodeInfo.IsReadOnlyReplica) {
				_fsm.Handle(new SystemMessage.BecomeReadOnlyLeaderless(Guid.NewGuid()));
			} else {
				_fsm.Handle(new SystemMessage.BecomeUnknown(Guid.NewGuid()));
			}
		}

		private void Handle(SystemMessage.BecomeUnknown message) {
			Log.Info("========== [{internalHttp}] IS UNKNOWN...", _nodeInfo.InternalHttp);

			_state = VNodeState.Unknown;
			_leader = null;
			_outputBus.Publish(message);
			_mainQueue.Publish(new ElectionMessage.StartElections());
		}
		
		private void Handle(SystemMessage.InitiateLeaderResignation message) {
			Log.Info("========== [{internalHttp}] IS INITIATING LEADER RESIGNATION...", _nodeInfo.InternalHttp);

			_fsm.Handle(new SystemMessage.BecomeResigningLeader(_stateCorrelationId));
		}

		private void Handle(SystemMessage.BecomeResigningLeader message) {
			Log.Info("========== [{internalHttp}] IS RESIGNING LEADER...", _nodeInfo.InternalHttp);
			if (_stateCorrelationId != message.CorrelationId)
				return;
			
			_state = VNodeState.ResigningLeader;
			_outputBus.Publish(message);
		}
		
		private void Handle(SystemMessage.RequestQueueDrained message) {
			Log.Info("========== [{internalHttp}] REQUEST QUEUE DRAINED. RESIGNATION COMPLETE.", _nodeInfo.InternalHttp);
			_fsm.Handle(new SystemMessage.BecomeUnknown(Guid.NewGuid()));
		}
		
		private void Handle(SystemMessage.BecomePreReplica message) {
			if (_leader == null) throw new Exception("_leader == null");
			if (_stateCorrelationId != message.CorrelationId)
				return;

			Log.Info(
				"========== [{internalHttp}] PRE-REPLICA STATE, WAITING FOR CHASER TO CATCH UP... LEADER IS [{leaderInternalHttp},{leaderId:B}]",
				_nodeInfo.InternalHttp, _leader.InternalHttp, _leader.InstanceId);
			_state = VNodeState.PreReplica;
			_outputBus.Publish(message);
			_mainQueue.Publish(new SystemMessage.WaitForChaserToCatchUp(_stateCorrelationId, TimeSpan.Zero));
		}

		private void Handle(SystemMessage.BecomePreReadOnlyReplica message) {
			if (_leader == null) throw new Exception("_leader == null");
			if (_stateCorrelationId != message.CorrelationId)
				return;

			Log.Info(
				"========== [{internalHttp}] READ ONLY PRE-REPLICA STATE, WAITING FOR CHASER TO CATCH UP... LEADER IS [{leaderInternalHttp},{leaderId:B}]",
				_nodeInfo.InternalHttp, _leader.InternalHttp, _leader.InstanceId);
			_state = VNodeState.PreReadOnlyReplica;
			_outputBus.Publish(message);
			_mainQueue.Publish(new SystemMessage.WaitForChaserToCatchUp(_stateCorrelationId, TimeSpan.Zero));
		}

		private void Handle(SystemMessage.BecomeCatchingUp message) {
			if (_leader == null) throw new Exception("_leader == null");
			if (_stateCorrelationId != message.CorrelationId)
				return;

			Log.Info("========== [{internalHttp}] IS CATCHING UP... LEADER IS [{leaderInternalHttp},{leaderId:B}]",
				_nodeInfo.InternalHttp, _leader.InternalHttp, _leader.InstanceId);
			_state = VNodeState.CatchingUp;
			_outputBus.Publish(message);
		}

		private void Handle(SystemMessage.BecomeClone message) {
			if (_leader == null) throw new Exception("_leader == null");
			if (_stateCorrelationId != message.CorrelationId)
				return;

			Log.Info("========== [{internalHttp}] IS CLONE... LEADER IS [{leaderInternalHttp},{leaderId:B}]",
				_nodeInfo.InternalHttp, _leader.InternalHttp, _leader.InstanceId);
			_state = VNodeState.Clone;
			_outputBus.Publish(message);
		}

		private void Handle(SystemMessage.BecomeSlave message) {
			if (_leader == null) throw new Exception("_leader == null");
			if (_stateCorrelationId != message.CorrelationId)
				return;

			Log.Info("========== [{internalHttp}] IS SLAVE... LEADER IS [{leaderInternalHttp},{leaderId:B}]",
				_nodeInfo.InternalHttp, _leader.InternalHttp, _leader.InstanceId);
			_state = VNodeState.Slave;
			_outputBus.Publish(message);
		}

		private void Handle(SystemMessage.BecomeReadOnlyLeaderless message) {
			Log.Info("========== [{internalHttp}] IS READ ONLY REPLICA WITH UNKNOWN LEADER...", _nodeInfo.InternalHttp);
			_state = VNodeState.ReadOnlyLeaderless;
			_leader = null;
			_outputBus.Publish(message);
			_mainQueue.Publish(new ElectionMessage.StartElections());
		}

		private void Handle(SystemMessage.BecomeReadOnlyReplica message) {
			if (_leader == null) throw new Exception("_leader == null");
			if (_stateCorrelationId != message.CorrelationId)
				return;

			Log.Info("========== [{internalHttp}] IS READ ONLY REPLICA... LEADER IS [{leaderInternalHttp},{leaderId:B}]",
				_nodeInfo.InternalHttp, _leader.InternalHttp, _leader.InstanceId);
			_state = VNodeState.ReadOnlyReplica;
			_outputBus.Publish(message);
		}

		private void Handle(SystemMessage.BecomePreLeader message) {
			if (_leader == null) throw new Exception("_leader == null");
			if (_stateCorrelationId != message.CorrelationId)
				return;

			Log.Info("========== [{internalHttp}] PRE-LEADER STATE, WAITING FOR CHASER TO CATCH UP...",
				_nodeInfo.InternalHttp);
			_state = VNodeState.PreLeader;
			_outputBus.Publish(message);
			_mainQueue.Publish(new SystemMessage.WaitForChaserToCatchUp(_stateCorrelationId, TimeSpan.Zero));
		}

		private void Handle(SystemMessage.BecomeLeader message) {
			if (_state == VNodeState.Leader) throw new Exception("We should not BecomeLeader twice in a row.");
			if (_leader == null) throw new Exception("_leader == null");
			if (_stateCorrelationId != message.CorrelationId)
				return;

			Log.Info("========== [{internalHttp}] IS LEADER... SPARTA!", _nodeInfo.InternalHttp);
			_state = VNodeState.Leader;
			_outputBus.Publish(message);
		}

		private void Handle(SystemMessage.BecomeShuttingDown message) {
			if (_state == VNodeState.ShuttingDown || _state == VNodeState.Shutdown)
				return;

			Log.Info("========== [{internalHttp}] IS SHUTTING DOWN...", _nodeInfo.InternalHttp);
			_leader = null;
			_stateCorrelationId = message.CorrelationId;
			_exitProcessOnShutdown = message.ExitProcess;
			_state = VNodeState.ShuttingDown;
			_mainQueue.Publish(TimerMessage.Schedule.Create(ShutdownTimeout, _publishEnvelope,
				new SystemMessage.ShutdownTimeout()));
			_outputBus.Publish(message);
		}

		private void Handle(SystemMessage.BecomeShutdown message) {
			Log.Info("========== [{internalHttp}] IS SHUT DOWN.", _nodeInfo.InternalHttp);
			_state = VNodeState.Shutdown;
			try {
				_outputBus.Publish(message);
			} catch (Exception exc) {
				Log.ErrorException(exc, "Error when publishing {message}.", message);
			}

			try {
				_node.WorkersHandler.Stop();
				_mainQueue.RequestStop();
			} catch (Exception exc) {
				Log.ErrorException(exc, "Error when stopping workers/main queue.");
			}

			if (_exitProcessOnShutdown) {
				Application.Exit(ExitCode.Success, "Shutdown and exit from process was requested.");
			}
		}

		private void Handle(ElectionMessage.ElectionsDone message) {
			if (_leader != null && _leader.InstanceId == message.Leader.InstanceId) {
				//if the leader hasn't changed, we skip state changes through PreLeader or PreReplica
				if (_leader.InstanceId == _nodeInfo.InstanceId && _state == VNodeState.Leader) {
					//transitioning from leader to leader, we just write a new epoch
					_fsm.Handle(new SystemMessage.WriteEpoch());
				}

				return;
			}

			_leader = VNodeInfoHelper.FromMemberInfo(message.Leader);
			_subscriptionId = Guid.NewGuid();
			_stateCorrelationId = Guid.NewGuid();
			_outputBus.Publish(message);
			if (_leader.InstanceId == _nodeInfo.InstanceId)
				_fsm.Handle(new SystemMessage.BecomePreLeader(_stateCorrelationId));
			else
				_fsm.Handle(new SystemMessage.BecomePreReplica(_stateCorrelationId, _leader));
		}

		private void HandleAsReadOnlyReplica(ElectionMessage.ElectionsDone message) {
			_leader = VNodeInfoHelper.FromMemberInfo(message.Leader);
			_subscriptionId = Guid.NewGuid();
			_stateCorrelationId = Guid.NewGuid();
			_outputBus.Publish(message);
			if (_leader != null) {
				_fsm.Handle(new SystemMessage.BecomePreReadOnlyReplica(_stateCorrelationId, _leader));
			}
		}

		private void Handle(SystemMessage.ServiceInitialized message) {
			Log.Info("========== [{internalHttp}] Service '{service}' initialized.", _nodeInfo.InternalHttp,
				message.ServiceName);
			_serviceInitsToExpect -= 1;
			_outputBus.Publish(message);
			if (_serviceInitsToExpect == 0)
				_mainQueue.Publish(new SystemMessage.SystemStart());
		}

		private void Handle(UserManagementMessage.UserManagementServiceInitialized message) {
			if (_subSystems != null) {
				foreach (var subsystem in _subSystems) {
					_node.AddTasks(subsystem.Start());
				}
			}

			_outputBus.Publish(message);
			_fsm.Handle(new SystemMessage.SystemCoreReady());
		}

		private void Handle(SystemMessage.SystemCoreReady message) {
			if (_subSystems == null || _subSystems.Length == 0) {
				_outputBus.Publish(new SystemMessage.SystemReady());
			} else {
				_outputBus.Publish(message);
			}
		}

		private void Handle(SystemMessage.SubSystemInitialized message) {
			Log.Info("========== [{internalHttp}] Sub System '{subSystemName}' initialized.", _nodeInfo.InternalHttp,
				message.SubSystemName);
			if (Interlocked.Decrement(ref _subSystemInitsToExpect) == 0) {
				_outputBus.Publish(new SystemMessage.SystemReady());
			}
		}

		private void HandleAsNonLeader(ClientMessage.ReadEvent message) {
			if (message.RequireLeader) {
				if (_leader == null)
					DenyRequestBecauseNotReady(message.Envelope, message.CorrelationId);
				else
					DenyRequestBecauseNotLeader(message.CorrelationId, message.Envelope);
			} else {
				_outputBus.Publish(message);
			}
		}

		private void HandleAsNonLeader(ClientMessage.ReadStreamEventsForward message) {
			if (message.RequireLeader) {
				if (_leader == null)
					DenyRequestBecauseNotReady(message.Envelope, message.CorrelationId);
				else
					DenyRequestBecauseNotLeader(message.CorrelationId, message.Envelope);
			} else {
				_outputBus.Publish(message);
			}
		}

		private void HandleAsNonLeader(ClientMessage.ReadStreamEventsBackward message) {
			if (message.RequireLeader) {
				if (_leader == null)
					DenyRequestBecauseNotReady(message.Envelope, message.CorrelationId);
				else
					DenyRequestBecauseNotLeader(message.CorrelationId, message.Envelope);
			} else {
				_outputBus.Publish(message);
			}
		}

		private void HandleAsNonLeader(ClientMessage.ReadAllEventsForward message) {
			if (message.RequireLeader) {
				if (_leader == null)
					DenyRequestBecauseNotReady(message.Envelope, message.CorrelationId);
				else
					DenyRequestBecauseNotLeader(message.CorrelationId, message.Envelope);
			} else {
				_outputBus.Publish(message);
			}
		}
		
		private void HandleAsNonLeader(ClientMessage.FilteredReadAllEventsForward message) {
			if (message.RequireLeader) {
				if (_leader == null)
					DenyRequestBecauseNotReady(message.Envelope, message.CorrelationId);
				else
					DenyRequestBecauseNotLeader(message.CorrelationId, message.Envelope);
			} else {
				_outputBus.Publish(message);
			}
		}

		private void HandleAsNonLeader(ClientMessage.ReadAllEventsBackward message) {
			if (message.RequireLeader) {
				if (_leader == null)
					DenyRequestBecauseNotReady(message.Envelope, message.CorrelationId);
				else
					DenyRequestBecauseNotLeader(message.CorrelationId, message.Envelope);
			} else {
				_outputBus.Publish(message);
			}
		}
		
		private void HandleAsNonLeader(ClientMessage.FilteredReadAllEventsBackward message) {
			if (message.RequireLeader) {
				if (_leader == null)
					DenyRequestBecauseNotReady(message.Envelope, message.CorrelationId);
				else
					DenyRequestBecauseNotLeader(message.CorrelationId, message.Envelope);
			} else {
				_outputBus.Publish(message);
			}
		}

		private void HandleAsNonLeader(ClientMessage.CreatePersistentSubscription message) {
			if (_leader == null)
				DenyRequestBecauseNotReady(message.Envelope, message.CorrelationId);
			else
				DenyRequestBecauseNotLeader(message.CorrelationId, message.Envelope);
		}

		private void HandleAsNonLeader(ClientMessage.ConnectToPersistentSubscription message) {
			if (_leader == null)
				DenyRequestBecauseNotReady(message.Envelope, message.CorrelationId);
			else
				DenyRequestBecauseNotLeader(message.CorrelationId, message.Envelope);
		}

		private void HandleAsNonLeader(ClientMessage.UpdatePersistentSubscription message) {
			if (_leader == null)
				DenyRequestBecauseNotReady(message.Envelope, message.CorrelationId);
			else
				DenyRequestBecauseNotLeader(message.CorrelationId, message.Envelope);
		}

		private void HandleAsNonLeader(ClientMessage.DeletePersistentSubscription message) {
			if (_leader == null)
				DenyRequestBecauseNotReady(message.Envelope, message.CorrelationId);
			else
				DenyRequestBecauseNotLeader(message.CorrelationId, message.Envelope);
		}

		private void HandleAsNonLeader(ClientMessage.WriteEvents message) {
			if (message.RequireLeader) {
				DenyRequestBecauseNotLeader(message.CorrelationId, message.Envelope);
				return;
			}

			var timeoutMessage = new ClientMessage.WriteEventsCompleted(
				message.CorrelationId, OperationResult.ForwardTimeout, "Forwarding timeout");
			ForwardRequest(message, timeoutMessage);
		}

		private void HandleAsNonLeader(ClientMessage.TransactionStart message) {
			if (message.RequireLeader) {
				DenyRequestBecauseNotLeader(message.CorrelationId, message.Envelope);
				return;
			}

			var timeoutMessage = new ClientMessage.TransactionStartCompleted(
				message.CorrelationId, -1, OperationResult.ForwardTimeout, "Forwarding timeout");
			ForwardRequest(message, timeoutMessage);
		}

		private void HandleAsNonLeader(ClientMessage.TransactionWrite message) {
			if (message.RequireLeader) {
				DenyRequestBecauseNotLeader(message.CorrelationId, message.Envelope);
				return;
			}

			var timeoutMessage = new ClientMessage.TransactionWriteCompleted(
				message.CorrelationId, message.TransactionId, OperationResult.ForwardTimeout, "Forwarding timeout");
			ForwardRequest(message, timeoutMessage);
		}

		private void HandleAsNonLeader(ClientMessage.TransactionCommit message) {
			if (message.RequireLeader) {
				DenyRequestBecauseNotLeader(message.CorrelationId, message.Envelope);
				return;
			}

			var timeoutMessage = new ClientMessage.TransactionCommitCompleted(
				message.CorrelationId, message.TransactionId, OperationResult.ForwardTimeout, "Forwarding timeout");
			ForwardRequest(message, timeoutMessage);
		}

		private void HandleAsNonLeader(ClientMessage.DeleteStream message) {
			if (message.RequireLeader) {
				DenyRequestBecauseNotLeader(message.CorrelationId, message.Envelope);
				return;
			}

			var timeoutMessage = new ClientMessage.DeleteStreamCompleted(
				message.CorrelationId, OperationResult.ForwardTimeout, "Forwarding timeout", -1, -1);
			ForwardRequest(message, timeoutMessage);
		}

		private void ForwardRequest(ClientMessage.WriteRequestMessage msg, Message timeoutMessage) {
			_forwardingProxy.Register(msg.InternalCorrId, msg.CorrelationId, msg.Envelope, _forwardingTimeout,
				timeoutMessage);
			_outputBus.Publish(new ClientMessage.TcpForwardMessage(msg));
		}

		private void DenyRequestBecauseNotLeader(Guid correlationId, IEnvelope envelope) {
			var leader = _leader ?? _nodeInfo;
			envelope.ReplyWith(
				new ClientMessage.NotHandled(correlationId,
					TcpClientMessageDto.NotHandled.NotHandledReason.NotLeader,
					new TcpClientMessageDto.NotHandled.LeaderInfo(leader.ExternalTcp,
						leader.ExternalSecureTcp,
						leader.ExternalHttp)));
		}

		private void HandleAsReadOnlyReplica(ClientMessage.WriteEvents message) {
			if (message.RequireLeader) {
				DenyRequestBecauseNotLeader(message.CorrelationId, message.Envelope);
				return;
			}
			if (message.User != SystemAccount.Principal) {
				DenyRequestBecauseReadOnly(message.CorrelationId, message.Envelope);
				return;
			}

			var timeoutMessage = new ClientMessage.WriteEventsCompleted(
				message.CorrelationId, OperationResult.ForwardTimeout, "Forwarding timeout");
			ForwardRequest(message, timeoutMessage);
		}

		private void HandleAsReadOnlyReplica(ClientMessage.TransactionStart message) {
			if (message.RequireLeader) {
				DenyRequestBecauseNotLeader(message.CorrelationId, message.Envelope);
				return;
			}
			if (message.User != SystemAccount.Principal) {
				DenyRequestBecauseReadOnly(message.CorrelationId, message.Envelope);
				return;
			}
			var timeoutMessage = new ClientMessage.TransactionStartCompleted(
				message.CorrelationId, -1, OperationResult.ForwardTimeout, "Forwarding timeout");
			ForwardRequest(message, timeoutMessage);
		}
			
		private void HandleAsReadOnlyReplica(ClientMessage.TransactionWrite message) {
			if (message.RequireLeader) {
				DenyRequestBecauseNotLeader(message.CorrelationId, message.Envelope);
				return;
			}
			if (message.User != SystemAccount.Principal) {
				DenyRequestBecauseReadOnly(message.CorrelationId, message.Envelope);
				return;
			}

			var timeoutMessage = new ClientMessage.TransactionWriteCompleted(
				message.CorrelationId, message.TransactionId, OperationResult.ForwardTimeout, "Forwarding timeout");
			ForwardRequest(message, timeoutMessage);
		}

		private void HandleAsReadOnlyReplica(ClientMessage.TransactionCommit message) {
			if (message.RequireLeader) {
				DenyRequestBecauseNotLeader(message.CorrelationId, message.Envelope);
				return;
			}
			if (message.User != SystemAccount.Principal) {
				DenyRequestBecauseReadOnly(message.CorrelationId, message.Envelope);
				return;
			}

			var timeoutMessage = new ClientMessage.TransactionCommitCompleted(
				message.CorrelationId, message.TransactionId, OperationResult.ForwardTimeout, "Forwarding timeout");
			ForwardRequest(message, timeoutMessage);
		}

		private void HandleAsReadOnlyReplica(ClientMessage.DeleteStream message) {
			if (message.RequireLeader) {
				DenyRequestBecauseNotLeader(message.CorrelationId, message.Envelope);
				return;
			}
			if (message.User != SystemAccount.Principal) {
				DenyRequestBecauseReadOnly(message.CorrelationId, message.Envelope);
				return;
			} 
			var timeoutMessage = new ClientMessage.DeleteStreamCompleted(
				message.CorrelationId, OperationResult.ForwardTimeout, "Forwarding timeout", -1, -1);
			ForwardRequest(message, timeoutMessage);
		}

		private void DenyRequestBecauseReadOnly(Guid correlationId, IEnvelope envelope) {
			var leader = _leader ?? _nodeInfo;
			envelope.ReplyWith(
				new ClientMessage.NotHandled(correlationId,
					TcpClientMessageDto.NotHandled.NotHandledReason.IsReadOnly,
					new TcpClientMessageDto.NotHandled.LeaderInfo(leader.ExternalTcp,
						leader.ExternalSecureTcp,
						leader.ExternalHttp)));
		}

		private void DenyRequestBecauseNotReady(IEnvelope envelope, Guid correlationId) {
			envelope.ReplyWith(new ClientMessage.NotHandled(correlationId,
				TcpClientMessageDto.NotHandled.NotHandledReason.NotReady, null));
		}

		private void Handle(SystemMessage.VNodeConnectionLost message) {
			if (_leader != null && _leader.Is(message.VNodeEndPoint)) // leader connection failed
			{
				var msg = _state == VNodeState.PreReplica
					? (Message)new ReplicationMessage.ReconnectToLeader(_stateCorrelationId, _leader)
					: new SystemMessage.BecomePreReplica(_stateCorrelationId, _leader);
				_mainQueue.Publish(TimerMessage.Schedule.Create(LeaderReconnectionDelay, _publishEnvelope, msg));
			}

			_outputBus.Publish(message);
		}

		private void HandleAsReadOnlyReplica(SystemMessage.VNodeConnectionLost message) {
			if (_leader != null && _leader.Is(message.VNodeEndPoint)) // leader connection failed
			{
				var msg = _state == VNodeState.PreReadOnlyReplica
					? (Message)new ReplicationMessage.ReconnectToLeader(_stateCorrelationId, _leader)
					: new SystemMessage.BecomePreReadOnlyReplica(_stateCorrelationId, _leader);
				_mainQueue.Publish(TimerMessage.Schedule.Create(LeaderReconnectionDelay, _publishEnvelope, msg));
			}

			_outputBus.Publish(message);
		}

		private void HandleAsLeader(GossipMessage.GossipUpdated message) {
			if (_leader == null) throw new Exception("_leader == null");
			if (message.ClusterInfo.Members.Count(x => x.IsAlive && x.State == VNodeState.Leader) > 1) {
				Log.Debug("There are MULTIPLE LEADERS according to gossip, need to start elections. LEADER: [{leader}]",
					_leader);
				Log.Debug("GOSSIP:");
				Log.Debug("{clusterInfo}", message.ClusterInfo);
				_mainQueue.Publish(new ElectionMessage.StartElections());
			}

			_outputBus.Publish(message);
		}

		private void HandleAsReadOnlyReplica(GossipMessage.GossipUpdated message) {
			if (_leader == null) throw new Exception("_leader == null");
			_outputBus.Publish(message);
		}

		private void HandleAsNonLeader(GossipMessage.GossipUpdated message) {
			if (_leader == null) throw new Exception("_leader == null");
			var leader = message.ClusterInfo.Members.FirstOrDefault(x => x.InstanceId == _leader.InstanceId);
			if (leader == null || !leader.IsAlive) {
				Log.Debug(
					"There is NO LEADER or LEADER is DEAD according to GOSSIP. Starting new elections. LEADER: [{leader}].",
					_leader);
				_mainQueue.Publish(new ElectionMessage.StartElections());
			}

			_outputBus.Publish(message);
		}

		private void Handle(SystemMessage.NoQuorumMessage message) {
			Log.Info("=== NO QUORUM EMERGED WITHIN TIMEOUT... RETIRING...");
			_fsm.Handle(new SystemMessage.BecomeUnknown(Guid.NewGuid()));
		}

		private void Handle(SystemMessage.WaitForChaserToCatchUp message) {
			if (message.CorrelationId != _stateCorrelationId)
				return;
			_outputBus.Publish(message);
		}

		private void HandleAsPreLeader(SystemMessage.ChaserCaughtUp message) {
			if (_leader == null) throw new Exception("_leader == null");
			if (_stateCorrelationId != message.CorrelationId)
				return;

			_outputBus.Publish(message);
			_fsm.Handle(new SystemMessage.BecomeLeader(_stateCorrelationId));
		}

		private void HandleAsPreReplica(SystemMessage.ChaserCaughtUp message) {
			if (_leader == null) throw new Exception("_leader == null");
			if (_stateCorrelationId != message.CorrelationId)
				return;
			_outputBus.Publish(message);
			_fsm.Handle(
				new ReplicationMessage.SubscribeToLeader(_stateCorrelationId, _leader.InstanceId, Guid.NewGuid()));
		}

		private void Handle(ReplicationMessage.ReconnectToLeader message) {
			if (_leader.InstanceId != message.Leader.InstanceId || _stateCorrelationId != message.StateCorrelationId)
				return;
			_outputBus.Publish(message);
		}

		private void Handle(ReplicationMessage.SubscribeToLeader message) {
			if (message.LeaderId != _leader.InstanceId || _stateCorrelationId != message.StateCorrelationId)
				return;
			_subscriptionId = message.SubscriptionId;
			_outputBus.Publish(message);

			var msg = new ReplicationMessage.SubscribeToLeader(_stateCorrelationId, _leader.InstanceId, Guid.NewGuid());
			_mainQueue.Publish(TimerMessage.Schedule.Create(LeaderSubscriptionTimeout, _publishEnvelope, msg));
		}

		private void Handle(ReplicationMessage.ReplicaSubscriptionRetry message) {
			if (IsLegitimateReplicationMessage(message)) {
				_outputBus.Publish(message);

				var msg = new ReplicationMessage.SubscribeToLeader(_stateCorrelationId, _leader.InstanceId,
					Guid.NewGuid());
				_mainQueue.Publish(TimerMessage.Schedule.Create(LeaderSubscriptionRetryDelay, _publishEnvelope, msg));
			}
		}

		private void Handle(ReplicationMessage.ReplicaSubscribed message) {
			if (IsLegitimateReplicationMessage(message)) {
				_outputBus.Publish(message);
				if (_nodeInfo.IsReadOnlyReplica) {
					_fsm.Handle(new SystemMessage.BecomeReadOnlyReplica(_stateCorrelationId, _leader));
				} else {
					_fsm.Handle(new SystemMessage.BecomeCatchingUp(_stateCorrelationId, _leader));
				}
			}
		}

		private void ForwardReplicationMessage<T>(T message) where T : Message, ReplicationMessage.IReplicationMessage {
			if (IsLegitimateReplicationMessage(message))
				_outputBus.Publish(message);
		}

		private void Handle(ReplicationMessage.SlaveAssignment message) {
			if (IsLegitimateReplicationMessage(message)) {
				Log.Info(
					"========== [{internalHttp}] SLAVE ASSIGNMENT RECEIVED FROM [{internalTcp},{internalSecureTcp},{leaderId:B}].",
					_nodeInfo.InternalHttp,
					_leader.InternalTcp,
					_leader.InternalSecureTcp == null ? "n/a" : _leader.InternalSecureTcp.ToString(),
					message.LeaderId);
				_outputBus.Publish(message);
				_fsm.Handle(new SystemMessage.BecomeSlave(_stateCorrelationId, _leader));
			}
		}

		private void Handle(ReplicationMessage.CloneAssignment message) {
			if (IsLegitimateReplicationMessage(message)) {
				Log.Info(
					"========== [{internalHttp}] CLONE ASSIGNMENT RECEIVED FROM [{internalTcp},{internalSecureTcp},{leaderId:B}].",
					_nodeInfo.InternalHttp,
					_leader.InternalTcp,
					_leader.InternalSecureTcp == null ? "n/a" : _leader.InternalSecureTcp.ToString(),
					message.LeaderId);
				_outputBus.Publish(message);
				_fsm.Handle(new SystemMessage.BecomeClone(_stateCorrelationId, _leader));
			}
		}

		private void Handle(ReplicationMessage.DropSubscription message) {
			if (IsLegitimateReplicationMessage(message)) {
				Log.Info(
					"========== [{internalHttp}] DROP SUBSCRIPTION REQUEST RECEIVED FROM [{internalTcp},{internalSecureTcp},{leaderId:B}]. THIS MEANS THAT THERE IS A SURPLUS OF NODES IN THE CLUSTER, SHUTTING DOWN.",
					_nodeInfo.InternalHttp,
					_leader.InternalTcp,
					_leader.InternalSecureTcp == null ? "n/a" : _leader.InternalSecureTcp.ToString(),
					message.LeaderId);
				_fsm.Handle(new ClientMessage.RequestShutdown(exitProcess: true, shutdownHttp: true));
			}
		}

		private bool IsLegitimateReplicationMessage(ReplicationMessage.IReplicationMessage message) {
			if (message.SubscriptionId == Guid.Empty)
				throw new Exception("IReplicationMessage with empty SubscriptionId provided.");
			if (message.SubscriptionId != _subscriptionId) {
				Log.Trace(
					"Ignoring {message} because SubscriptionId {receivedSubscriptionId:B} is wrong. Current SubscriptionId is {subscriptionId:B}.",
					message.GetType().Name, message.SubscriptionId, _subscriptionId);
				return false;
			}

			if (_leader == null || _leader.InstanceId != message.LeaderId) {
				var msg = string.Format("{0} message passed SubscriptionId check, but leader is either null or wrong. "
				                        + "Message.Leader: [{1:B}], VNode Leader: {2}.",
					message.GetType().Name, message.LeaderId, _leader);
				Log.Fatal("{messageType} message passed SubscriptionId check, but leader is either null or wrong. "
				          + "Message.Leader: [{leaderId:B}], VNode Leader: {leaderInfo}.",
					message.GetType().Name, message.LeaderId, _leader);
				Application.Exit(ExitCode.Error, msg);
				return false;
			}

			return true;
		}

		private void Handle(ClientMessage.RequestShutdown message) {
			_outputBus.Publish(message);
			_fsm.Handle(
				new SystemMessage.BecomeShuttingDown(Guid.NewGuid(), message.ExitProcess, message.ShutdownHttp));
		}

		private void Handle(SystemMessage.ServiceShutdown message) {
			Log.Info("========== [{internalHttp}] Service '{service}' has shut down.", _nodeInfo.InternalHttp,
				message.ServiceName);

			_serviceShutdownsToExpect -= 1;
			if (_serviceShutdownsToExpect == 0) {
				Log.Info("========== [{internalHttp}] All Services Shutdown.", _nodeInfo.InternalHttp);
				Shutdown();
			}

			_outputBus.Publish(message);
		}

		private void Handle(SystemMessage.ShutdownTimeout message) {
			Debug.Assert(_state == VNodeState.ShuttingDown);

			Log.Error("========== [{internalHttp}] Shutdown Timeout.", _nodeInfo.InternalHttp);
			Shutdown();
			_outputBus.Publish(message);
		}

		private void Shutdown() {
			Debug.Assert(_state == VNodeState.ShuttingDown);

			_db.Close();
			_fsm.Handle(new SystemMessage.BecomeShutdown(_stateCorrelationId));
		}
	}
}
