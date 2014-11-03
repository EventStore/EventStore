
using System;
using System.Diagnostics;
using System.Linq;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Cluster;
using EventStore.Core.Cluster.Settings;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.TransactionLog.Chunks;

namespace EventStore.Core.Services.VNode
{
    public class ClusterVNodeController : IHandle<Message>
    {
        public static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(5);
        public static readonly TimeSpan MasterReconnectionDelay = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan MasterSubscriptionRetryDelay = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan MasterSubscriptionTimeout = TimeSpan.FromMilliseconds(1000);

        private static readonly ILogger Log = LogManager.GetLoggerFor<ClusterVNodeController>();

        private readonly IPublisher _outputBus;
        private readonly VNodeInfo _nodeInfo;
        private readonly TFChunkDb _db;
        private readonly ClusterVNode _node;

        private VNodeState _state = VNodeState.Initializing;
        private VNodeInfo _master;
        private Guid _stateCorrelationId = Guid.NewGuid();
        private Guid _subscriptionId = Guid.Empty;

        private QueuedHandler _mainQueue;
        private IEnvelope _publishEnvelope;
        private readonly VNodeFSM _fsm;

        private readonly MessageForwardingProxy _forwardingProxy;
        private readonly TimeSpan _forwardingTimeout;

        private int _serviceInitsToExpect = 1 /* StorageChaser */
                                          + 1 /* StorageReader */
                                          + 1 /* StorageWriter */;
        private int _serviceShutdownsToExpect = 1 /* StorageChaser */
                                              + 1 /* StorageReader */
                                              + 1 /* StorageWriter */
                                              + 1 /* MasterReplicationService */
                                              + 1 /* HttpService Internal*/
                                              + 1 /* HttpService External*/;
        private bool _exitProcessOnShutdown;

        public ClusterVNodeController(IPublisher outputBus, VNodeInfo nodeInfo, TFChunkDb db,
                                      ClusterVNodeSettings vnodeSettings, ClusterVNode node,
                                      MessageForwardingProxy forwardingProxy)
        {
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
            if(vnodeSettings.ClusterNodeCount == 1) {
                _serviceShutdownsToExpect = 4;
            }

            _forwardingProxy = forwardingProxy;
            _forwardingTimeout = vnodeSettings.PrepareTimeout + vnodeSettings.CommitTimeout + TimeSpan.FromMilliseconds(300);

            _fsm = CreateFSM();
        }

        public void SetMainQueue(QueuedHandler mainQueue)
        {
            Ensure.NotNull(mainQueue, "mainQueue");

            _mainQueue = mainQueue;
            _publishEnvelope = new PublishEnvelope(mainQueue);
        }

        private VNodeFSM CreateFSM()
        {
            var stm = new VNodeFSMBuilder(() => _state)
                .InAnyState()
                    .When<SystemMessage.StateChangeMessage>()
                        .Do(m => Application.Exit(ExitCode.Error, string.Format("{0} message was unhandled in {1}.", m.GetType().Name, GetType().Name)))

                .InState(VNodeState.Initializing)
                    .When<SystemMessage.SystemInit>().Do(Handle)
                    .When<SystemMessage.SystemStart>().Do(Handle)
                    .When<SystemMessage.ServiceInitialized>().Do(Handle)
                    .When<ClientMessage.ScavengeDatabase>().Ignore()
                    .WhenOther().ForwardTo(_outputBus)

                .InState(VNodeState.Unknown)
                    .WhenOther().ForwardTo(_outputBus)

                .InStates(VNodeState.Initializing, VNodeState.Master, VNodeState.PreMaster,
                          VNodeState.PreReplica, VNodeState.CatchingUp, VNodeState.Clone, VNodeState.Slave)
                    .When<SystemMessage.BecomeUnknown>().Do(Handle)

                .InAllStatesExcept(VNodeState.Unknown,
                                   VNodeState.PreReplica, VNodeState.CatchingUp, VNodeState.Clone, VNodeState.Slave,
                                   VNodeState.Master)
                    .When<ClientMessage.ReadRequestMessage>().Do(msg => DenyRequestBecauseNotReady(msg.Envelope, msg.CorrelationId))

                .InAllStatesExcept(VNodeState.Master,
                                   VNodeState.PreReplica, VNodeState.CatchingUp, VNodeState.Clone, VNodeState.Slave)
                    .When<ClientMessage.WriteRequestMessage>().Do(msg => DenyRequestBecauseNotReady(msg.Envelope, msg.CorrelationId))

                .InState(VNodeState.Master)
                    .When<ClientMessage.ReadEvent>().ForwardTo(_outputBus)
                    .When<ClientMessage.ReadStreamEventsForward>().ForwardTo(_outputBus)
                    .When<ClientMessage.ReadStreamEventsBackward>().ForwardTo(_outputBus)
                    .When<ClientMessage.ReadAllEventsForward>().ForwardTo(_outputBus)
                    .When<ClientMessage.ReadAllEventsBackward>().ForwardTo(_outputBus)
                    .When<ClientMessage.WriteEvents>().ForwardTo(_outputBus)
                    .When<ClientMessage.TransactionStart>().ForwardTo(_outputBus)
                    .When<ClientMessage.TransactionWrite>().ForwardTo(_outputBus)
                    .When<ClientMessage.TransactionCommit>().ForwardTo(_outputBus)
                    .When<ClientMessage.DeleteStream>().ForwardTo(_outputBus)

                .InStates(VNodeState.PreReplica, VNodeState.CatchingUp, VNodeState.Clone, VNodeState.Slave,
                          VNodeState.Unknown)
                    .When<ClientMessage.ReadEvent>().Do(HandleAsNonMaster)
                    .When<ClientMessage.ReadStreamEventsForward>().Do(HandleAsNonMaster)
                    .When<ClientMessage.ReadStreamEventsBackward>().Do(HandleAsNonMaster)
                    .When<ClientMessage.ReadAllEventsForward>().Do(HandleAsNonMaster)
                    .When<ClientMessage.ReadAllEventsBackward>().Do(HandleAsNonMaster)

                .InStates(VNodeState.PreReplica, VNodeState.CatchingUp, VNodeState.Clone, VNodeState.Slave)
                    .When<ClientMessage.WriteEvents>().Do(HandleAsNonMaster)
                    .When<ClientMessage.TransactionStart>().Do(HandleAsNonMaster)
                    .When<ClientMessage.TransactionWrite>().Do(HandleAsNonMaster)
                    .When<ClientMessage.TransactionCommit>().Do(HandleAsNonMaster)
                    .When<ClientMessage.DeleteStream>().Do(HandleAsNonMaster)

                .InAnyState()
                    .When<ClientMessage.NotHandled>().ForwardTo(_outputBus)
                    .When<ClientMessage.ReadEventCompleted>().ForwardTo(_outputBus)
                    .When<ClientMessage.ReadStreamEventsForwardCompleted>().ForwardTo(_outputBus)
                    .When<ClientMessage.ReadStreamEventsBackwardCompleted>().ForwardTo(_outputBus)
                    .When<ClientMessage.ReadAllEventsForwardCompleted>().ForwardTo(_outputBus)
                    .When<ClientMessage.ReadAllEventsBackwardCompleted>().ForwardTo(_outputBus)
                    .When<ClientMessage.WriteEventsCompleted>().ForwardTo(_outputBus)
                    .When<ClientMessage.TransactionStartCompleted>().ForwardTo(_outputBus)
                    .When<ClientMessage.TransactionWriteCompleted>().ForwardTo(_outputBus)
                    .When<ClientMessage.TransactionCommitCompleted>().ForwardTo(_outputBus)
                    .When<ClientMessage.DeleteStreamCompleted>().ForwardTo(_outputBus)

                .InAllStatesExcept(VNodeState.Initializing, VNodeState.ShuttingDown, VNodeState.Shutdown)
                    .When<ElectionMessage.ElectionsDone>().Do(Handle)

                .InStates(VNodeState.Unknown,
                          VNodeState.PreReplica, VNodeState.CatchingUp, VNodeState.Clone, VNodeState.Slave,
                          VNodeState.PreMaster, VNodeState.Master)
                    .When<SystemMessage.BecomePreReplica>().Do(Handle)
                    .When<SystemMessage.BecomePreMaster>().Do(Handle)

                .InStates(VNodeState.PreReplica, VNodeState.CatchingUp, VNodeState.Clone, VNodeState.Slave)
                    .When<GossipMessage.GossipUpdated>().Do(HandleAsNonMaster)
                    .When<SystemMessage.VNodeConnectionLost>().Do(Handle)

                .InAllStatesExcept(VNodeState.PreReplica, VNodeState.PreMaster)
                    .When<SystemMessage.WaitForChaserToCatchUp>().Ignore()
                    .When<SystemMessage.ChaserCaughtUp>().Ignore()

                .InState(VNodeState.PreReplica)
                    .When<SystemMessage.BecomeCatchingUp>().Do(Handle)
                    .When<SystemMessage.WaitForChaserToCatchUp>().Do(Handle)
                    .When<SystemMessage.ChaserCaughtUp>().Do(HandleAsPreReplica)
                    .When<ReplicationMessage.ReconnectToMaster>().Do(Handle)
                    .When<ReplicationMessage.SubscribeToMaster>().Do(Handle)
                    .When<ReplicationMessage.ReplicaSubscriptionRetry>().Do(Handle) 
                    .When<ReplicationMessage.ReplicaSubscribed>().Do(Handle)
                    .WhenOther().ForwardTo(_outputBus)
                .InAllStatesExcept(VNodeState.PreReplica)
                    .When<ReplicationMessage.ReconnectToMaster>().Ignore()
                    .When<ReplicationMessage.SubscribeToMaster>().Ignore()
                    .When<ReplicationMessage.ReplicaSubscriptionRetry>().Ignore()
                    .When<ReplicationMessage.ReplicaSubscribed>().Ignore()

                .InStates(VNodeState.CatchingUp, VNodeState.Clone, VNodeState.Slave)
                    .When<ReplicationMessage.CreateChunk>().Do(ForwardReplicationMessage)
                    .When<ReplicationMessage.RawChunkBulk>().Do(ForwardReplicationMessage)
                    .When<ReplicationMessage.DataChunkBulk>().Do(ForwardReplicationMessage)
                    .When<ReplicationMessage.AckLogPosition>().ForwardTo(_outputBus)
                    .WhenOther().ForwardTo(_outputBus)
                .InAllStatesExcept(VNodeState.CatchingUp, VNodeState.Clone, VNodeState.Slave)
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
                    .When<ReplicationMessage.SlaveAssignment>().Do(Handle)
                    .When<SystemMessage.BecomeSlave>().Do(Handle)
                .InState(VNodeState.Slave)
                    .When<ReplicationMessage.CloneAssignment>().Do(Handle)
                    .When<SystemMessage.BecomeClone>().Do(Handle)

                .InStates(VNodeState.PreMaster, VNodeState.Master)
                    .When<GossipMessage.GossipUpdated>().Do(HandleAsMaster)
                    .When<ReplicationMessage.ReplicaSubscriptionRequest>().ForwardTo(_outputBus)
                    .When<ReplicationMessage.ReplicaLogPositionAck>().ForwardTo(_outputBus)
                .InAllStatesExcept(VNodeState.PreMaster, VNodeState.Master)
                    .When<ReplicationMessage.ReplicaSubscriptionRequest>().Ignore()

                .InState(VNodeState.PreMaster)
                    .When<SystemMessage.BecomeMaster>().Do(Handle)
                    .When<SystemMessage.WaitForChaserToCatchUp>().Do(Handle)
                    .When<SystemMessage.ChaserCaughtUp>().Do(HandleAsPreMaster)
                    .WhenOther().ForwardTo(_outputBus)

                .InState(VNodeState.Master)
                    .When<SystemMessage.NoQuorumMessage>().Do(Handle)
                    .When<StorageMessage.WritePrepares>().ForwardTo(_outputBus)
                    .When<StorageMessage.WriteDelete>().ForwardTo(_outputBus)
                    .When<StorageMessage.WriteTransactionStart>().ForwardTo(_outputBus)
                    .When<StorageMessage.WriteTransactionData>().ForwardTo(_outputBus)
                    .When<StorageMessage.WriteTransactionPrepare>().ForwardTo(_outputBus)
                    .When<StorageMessage.WriteCommit>().ForwardTo(_outputBus)
                    .WhenOther().ForwardTo(_outputBus)

                .InAllStatesExcept(VNodeState.Master)
                    .When<SystemMessage.NoQuorumMessage>().Ignore()
                    .When<StorageMessage.WritePrepares>().Ignore()
                    .When<StorageMessage.WriteDelete>().Ignore()
                    .When<StorageMessage.WriteTransactionStart>().Ignore()
                    .When<StorageMessage.WriteTransactionData>().Ignore()
                    .When<StorageMessage.WriteTransactionPrepare>().Ignore()
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

        void IHandle<Message>.Handle(Message message)
        {
            _fsm.Handle(message);
        }

        private void Handle(SystemMessage.SystemInit message)
        {
            Log.Info("========== [{0}] SYSTEM INIT...", _nodeInfo.InternalHttp);
            _outputBus.Publish(message);
        }

        private void Handle(SystemMessage.SystemStart message)
        {
            Log.Info("========== [{0}] SYSTEM START...", _nodeInfo.InternalHttp);
            _outputBus.Publish(message);
            _fsm.Handle(new SystemMessage.BecomeUnknown(Guid.NewGuid()));
        }

        private void Handle(SystemMessage.BecomeUnknown message)
        {
            Log.Info("========== [{0}] IS UNKNOWN!!! WHOA!!!", _nodeInfo.InternalHttp);
           
            _state = VNodeState.Unknown;
            _master = null;           
            _outputBus.Publish(message);
            _mainQueue.Publish(new ElectionMessage.StartElections());
        }

        private void Handle(SystemMessage.BecomePreReplica message)
        {
            if (_master == null) throw new Exception("_master == null");
            if (_stateCorrelationId != message.CorrelationId)
                return;

            Log.Info("========== [{0}] PRE-REPLICA STATE, WAITING FOR CHASER TO CATCH UP... MASTER IS [{1},{2:B}]",
                     _nodeInfo.InternalHttp, _master.InternalHttp, _master.InstanceId);
            _state = VNodeState.PreReplica;
            _outputBus.Publish(message);
            _mainQueue.Publish(new SystemMessage.WaitForChaserToCatchUp(_stateCorrelationId, TimeSpan.Zero));
        }

        private void Handle(SystemMessage.BecomeCatchingUp message)
        {
            if (_master == null) throw new Exception("_master == null");
            if (_stateCorrelationId != message.CorrelationId)
                return;

            Log.Info("========== [{0}] IS CATCHING UP!!! BANZAI!!! MASTER IS [{1},{2:B}]",
                     _nodeInfo.InternalHttp, _master.InternalHttp, _master.InstanceId);
            _state = VNodeState.CatchingUp;
            _outputBus.Publish(message);
        }

        private void Handle(SystemMessage.BecomeClone message)
        {
            if (_master == null) throw new Exception("_master == null");
            if (_stateCorrelationId != message.CorrelationId)
                return;

            Log.Info("========== [{0}] IS CLONE!!! SPARTA!!! MASTER IS [{1},{2:B}]",
                     _nodeInfo.InternalHttp, _master.InternalHttp, _master.InstanceId);
            _state = VNodeState.Clone;
            _outputBus.Publish(message);
        }

        private void Handle(SystemMessage.BecomeSlave message)
        {
            if (_master == null) throw new Exception("_master == null");
            if (_stateCorrelationId != message.CorrelationId)
                return;

            Log.Info("========== [{0}] IS SLAVE!!! SPARTA!!! MASTER IS [{1},{2:B}]",
                     _nodeInfo.InternalHttp, _master.InternalHttp, _master.InstanceId);
            _state = VNodeState.Slave;
            _outputBus.Publish(message);
        }

        private void Handle(SystemMessage.BecomePreMaster message)
        {
            if (_master == null) throw new Exception("_master == null");
            if (_stateCorrelationId != message.CorrelationId)
                return;

            Log.Info("========== [{0}] PRE-MASTER STATE, WAITING FOR CHASER TO CATCH UP...", _nodeInfo.InternalHttp);
            _state = VNodeState.PreMaster;
            _outputBus.Publish(message);
            _mainQueue.Publish(new SystemMessage.WaitForChaserToCatchUp(_stateCorrelationId, TimeSpan.Zero));
        }

        private void Handle(SystemMessage.BecomeMaster message)
        {
            if (_state == VNodeState.Master) throw new Exception("We should not BecomeMaster twice in a row.");
            if (_master == null) throw new Exception("_master == null");
            if (_stateCorrelationId != message.CorrelationId)
                return;

            Log.Info("========== [{0}] IS MASTER!!! SPARTA!!!", _nodeInfo.InternalHttp);
            _state = VNodeState.Master;
            _outputBus.Publish(message);
        }

        private void Handle(SystemMessage.BecomeShuttingDown message)
        {
            if (_state == VNodeState.ShuttingDown || _state == VNodeState.Shutdown)
                return;

            Log.Info("========== [{0}] IS SHUTTING DOWN!!! FAREWELL, WORLD...", _nodeInfo.InternalHttp);
            _master = null;
            _stateCorrelationId = message.CorrelationId;
            _exitProcessOnShutdown = message.ExitProcess;
            _state = VNodeState.ShuttingDown;
            _mainQueue.Publish(TimerMessage.Schedule.Create(ShutdownTimeout, _publishEnvelope, new SystemMessage.ShutdownTimeout()));
            _outputBus.Publish(message);
        }

        private void Handle(SystemMessage.BecomeShutdown message)
        {
            Log.Info("========== [{0}] IS SHUT DOWN!!! SWEET DREAMS!!!", _nodeInfo.InternalHttp);
            _state = VNodeState.Shutdown;
            try
            {
                _outputBus.Publish(message);
            }
            catch (Exception exc)
            {
                Log.ErrorException(exc, "Error when publishing {0}.", message);
            }
            if (_exitProcessOnShutdown)
            {
                try
                {
                    _node.WorkersHandler.Stop();
                    _mainQueue.RequestStop();
                }
                catch (Exception exc)
                {
                    Log.ErrorException(exc, "Error when stopping workers/main queue.");
                }
                Application.Exit(ExitCode.Success, "Shutdown with exiting from process was requested.");
            }
        }

        private void Handle(ElectionMessage.ElectionsDone message)
        {
            if (_master != null && _master.InstanceId == message.Master.InstanceId)
            {
                if (_master.InstanceId == _nodeInfo.InstanceId)
                    _fsm.Handle(new SystemMessage.WriteEpoch());
                return;
            }

            _master = VNodeInfoHelper.FromMemberInfo(message.Master);
            _subscriptionId = Guid.NewGuid();
            _stateCorrelationId = Guid.NewGuid();
            _outputBus.Publish(message);
            if (_master.InstanceId == _nodeInfo.InstanceId)
                _fsm.Handle(new SystemMessage.BecomePreMaster(_stateCorrelationId));
            else
                _fsm.Handle(new SystemMessage.BecomePreReplica(_stateCorrelationId, _master));
        }

        private void Handle(SystemMessage.ServiceInitialized message)
        {
            Log.Info("========== [{0}] Service '{1}' initialized.", _nodeInfo.InternalHttp, message.ServiceName);
            _serviceInitsToExpect -= 1;
            _outputBus.Publish(message);
            if (_serviceInitsToExpect == 0)
                _mainQueue.Publish(new SystemMessage.SystemStart());
        }

        private void HandleAsNonMaster(ClientMessage.ReadEvent message)
        {
            if (message.RequireMaster)
            {
                if (_master == null)
                    DenyRequestBecauseNotReady(message.Envelope, message.CorrelationId);
                else
                    DenyRequestBecauseNotMaster(message.CorrelationId, message.Envelope);
            }
            else
            {
                _outputBus.Publish(message);
            }
        }

        private void HandleAsNonMaster(ClientMessage.ReadStreamEventsForward message)
        {
            if (message.RequireMaster)
            {
                if (_master == null)
                    DenyRequestBecauseNotReady(message.Envelope, message.CorrelationId);
                else
                    DenyRequestBecauseNotMaster(message.CorrelationId, message.Envelope);
            }
            else
            {
                _outputBus.Publish(message);
            }
        }

        private void HandleAsNonMaster(ClientMessage.ReadStreamEventsBackward message)
        {
            if (message.RequireMaster)
            {
                if (_master == null)
                    DenyRequestBecauseNotReady(message.Envelope, message.CorrelationId);
                else
                    DenyRequestBecauseNotMaster(message.CorrelationId, message.Envelope);
            }
            else
            {
                _outputBus.Publish(message);
            }
        }

        private void HandleAsNonMaster(ClientMessage.ReadAllEventsForward message)
        {
            if (message.RequireMaster)
            {
                if (_master == null)
                    DenyRequestBecauseNotReady(message.Envelope, message.CorrelationId);
                else
                    DenyRequestBecauseNotMaster(message.CorrelationId, message.Envelope);
            }
            else
            {
                _outputBus.Publish(message);
            }
        }

        private void HandleAsNonMaster(ClientMessage.ReadAllEventsBackward message)
        {
            if (message.RequireMaster)
            {
                if (_master == null)
                    DenyRequestBecauseNotReady(message.Envelope, message.CorrelationId);
                else
                    DenyRequestBecauseNotMaster(message.CorrelationId, message.Envelope);
            }
            else
            {
                _outputBus.Publish(message);
            }
        }

        private void HandleAsNonMaster(ClientMessage.WriteEvents message)
        {
            if (message.RequireMaster)
            {
                DenyRequestBecauseNotMaster(message.CorrelationId, message.Envelope);
                return;
            }
            var timeoutMessage = new ClientMessage.WriteEventsCompleted(
                    message.CorrelationId, OperationResult.ForwardTimeout, "Forwarding timeout");
            ForwardRequest(message, timeoutMessage);
        }

        private void HandleAsNonMaster(ClientMessage.TransactionStart message)
        {
            if (message.RequireMaster)
            {
                DenyRequestBecauseNotMaster(message.CorrelationId, message.Envelope);
                return;
            }
            var timeoutMessage = new ClientMessage.TransactionStartCompleted(
                message.CorrelationId, -1, OperationResult.ForwardTimeout, "Forwarding timeout");
            ForwardRequest(message, timeoutMessage);
        }

        private void HandleAsNonMaster(ClientMessage.TransactionWrite message)
        {
            if (message.RequireMaster)
            {
                DenyRequestBecauseNotMaster(message.CorrelationId, message.Envelope);
                return;
            }
            var timeoutMessage = new ClientMessage.TransactionWriteCompleted(
                message.CorrelationId, message.TransactionId, OperationResult.ForwardTimeout, "Forwarding timeout");
            ForwardRequest(message, timeoutMessage);
        }

        private void HandleAsNonMaster(ClientMessage.TransactionCommit message)
        {
            if (message.RequireMaster)
            {
                DenyRequestBecauseNotMaster(message.CorrelationId, message.Envelope);
                return;
            }
            var timeoutMessage = new ClientMessage.TransactionCommitCompleted(
                message.CorrelationId, message.TransactionId, OperationResult.ForwardTimeout, "Forwarding timeout");
            ForwardRequest(message, timeoutMessage);
        }

        private void HandleAsNonMaster(ClientMessage.DeleteStream message)
        {
            if (message.RequireMaster)
            {
                DenyRequestBecauseNotMaster(message.CorrelationId, message.Envelope);
                return;
            }
            var timeoutMessage = new ClientMessage.DeleteStreamCompleted(
                message.CorrelationId, OperationResult.ForwardTimeout, "Forwarding timeout", -1, -1);
            ForwardRequest(message, timeoutMessage);
        }

        private void ForwardRequest(ClientMessage.WriteRequestMessage msg, Message timeoutMessage)
        {
            _forwardingProxy.Register(msg.InternalCorrId, msg.CorrelationId, msg.Envelope, _forwardingTimeout, timeoutMessage);
            _outputBus.Publish(new ClientMessage.TcpForwardMessage(msg));
        }

        private void DenyRequestBecauseNotMaster(Guid correlationId, IEnvelope envelope)
        {
            var master = _master ?? _nodeInfo;
            envelope.ReplyWith(
                new ClientMessage.NotHandled(correlationId,
                                             TcpClientMessageDto.NotHandled.NotHandledReason.NotMaster,
                                             new TcpClientMessageDto.NotHandled.MasterInfo(master.ExternalTcp,
                                                                                           master.ExternalSecureTcp,
                                                                                           master.ExternalHttp)));
        }

        private void DenyRequestBecauseNotReady(IEnvelope envelope, Guid correlationId)
        {
            envelope.ReplyWith(new ClientMessage.NotHandled(correlationId, TcpClientMessageDto.NotHandled.NotHandledReason.NotReady, null));
        }

        private void Handle(SystemMessage.VNodeConnectionLost message)
        {
            if (_master != null && _master.Is(message.VNodeEndPoint)) // master connection failed
            {
                var msg = _state == VNodeState.PreReplica
                                  ? (Message) new ReplicationMessage.ReconnectToMaster(_stateCorrelationId, _master)
                                  : new SystemMessage.BecomePreReplica(_stateCorrelationId, _master);
                _mainQueue.Publish(TimerMessage.Schedule.Create(MasterReconnectionDelay, _publishEnvelope, msg));
            }
            _outputBus.Publish(message);
        }

        private void HandleAsMaster(GossipMessage.GossipUpdated message)
        {
            if (_master == null) throw new Exception("_master == null");
            if (message.ClusterInfo.Members.Count(x => x.IsAlive && x.State == VNodeState.Master) > 1)
            {
                Log.Debug("There are FEW MASTERS according to gossip, need to start elections. MASTER: [{0}].\nGOSSIP:\n{1}", 
                          _master, message.ClusterInfo);
                _mainQueue.Publish(new ElectionMessage.StartElections());
            }

            _outputBus.Publish(message);
        }

        private void HandleAsNonMaster(GossipMessage.GossipUpdated message)
        {
            if (_master == null) throw new Exception("_master == null");
            var master = message.ClusterInfo.Members.FirstOrDefault(x => x.InstanceId == _master.InstanceId);
            if (master == null || !master.IsAlive)
            {
                Log.Debug("There is NO MASTER or MASTER is DEAD according to GOSSIP. Starting new elections. MASTER: [{0}].", _master);
                _mainQueue.Publish(new ElectionMessage.StartElections());
            }

            _outputBus.Publish(message);
        }

        private void Handle(SystemMessage.NoQuorumMessage message)
        {
            Log.Info("=== NO QUORUM EMERGED WITHIN TIMEOUT... RETIRING...");
            _fsm.Handle(new SystemMessage.BecomeUnknown(Guid.NewGuid()));
        }

        private void Handle(SystemMessage.WaitForChaserToCatchUp message)
        {
            if (message.CorrelationId != _stateCorrelationId)
                return;
            _outputBus.Publish(message);
        }

        private void HandleAsPreMaster(SystemMessage.ChaserCaughtUp message)
        {
            if (_master == null) throw new Exception("_master == null");
            if (_stateCorrelationId != message.CorrelationId)
                return;

            _outputBus.Publish(message);
            _fsm.Handle(new SystemMessage.BecomeMaster(_stateCorrelationId));
        }

        private void HandleAsPreReplica(SystemMessage.ChaserCaughtUp message)
        {
            if (_master == null) throw new Exception("_master == null");
            if (_stateCorrelationId != message.CorrelationId)
                return;
            _outputBus.Publish(message);
            _fsm.Handle(new ReplicationMessage.SubscribeToMaster(_stateCorrelationId, _master.InstanceId, Guid.NewGuid()));
        }

        private void Handle(ReplicationMessage.ReconnectToMaster message)
        {
            if (_master.InstanceId != message.Master.InstanceId || _stateCorrelationId != message.StateCorrelationId)
                return;
            _outputBus.Publish(message);
        }

        private void Handle(ReplicationMessage.SubscribeToMaster message)
        {
            if (message.MasterId != _master.InstanceId || _stateCorrelationId != message.StateCorrelationId)
                return;
            _subscriptionId = message.SubscriptionId;
            _outputBus.Publish(message);

            var msg = new ReplicationMessage.SubscribeToMaster(_stateCorrelationId, _master.InstanceId, Guid.NewGuid());
            _mainQueue.Publish(TimerMessage.Schedule.Create(MasterSubscriptionTimeout, _publishEnvelope, msg));
        }

        private void Handle(ReplicationMessage.ReplicaSubscriptionRetry message)
        {
            if (IsLegitimateReplicationMessage(message))
            {
                _outputBus.Publish(message);

                var msg = new ReplicationMessage.SubscribeToMaster(_stateCorrelationId, _master.InstanceId, Guid.NewGuid());
                _mainQueue.Publish(TimerMessage.Schedule.Create(MasterSubscriptionRetryDelay, _publishEnvelope, msg));
            }
        }

        private void Handle(ReplicationMessage.ReplicaSubscribed message)
        {
            if (IsLegitimateReplicationMessage(message))
            {
                _outputBus.Publish(message);
                _fsm.Handle(new SystemMessage.BecomeCatchingUp(_stateCorrelationId, _master));
            }
        }

        private void ForwardReplicationMessage<T>(T message) where T : Message, ReplicationMessage.IReplicationMessage
        {
            if (IsLegitimateReplicationMessage(message))
                _outputBus.Publish(message);
        }

        private void Handle(ReplicationMessage.SlaveAssignment message)
        {
            if (IsLegitimateReplicationMessage(message))
            {
                Log.Info("========== [{0}] SLAVE ASSIGNMENT RECEIVED FROM [{1},{2},{3:B}].",
                         _nodeInfo.InternalHttp,
                         _master.InternalTcp, _master.InternalSecureTcp == null ? "n/a" : _master.InternalSecureTcp.ToString(),
                         message.MasterId);
                _outputBus.Publish(message);
                _fsm.Handle(new SystemMessage.BecomeSlave(_stateCorrelationId, _master));
            }
        }

        private void Handle(ReplicationMessage.CloneAssignment message)
        {
            if (IsLegitimateReplicationMessage(message))
            {
                Log.Info("========== [{0}] CLONE ASSIGNMENT RECEIVED FROM [{1},{2},{3:B}].",
                         _nodeInfo.InternalHttp,
                         _master.InternalTcp, _master.InternalSecureTcp == null ? "n/a" : _master.InternalSecureTcp.ToString(),
                         message.MasterId);
                _outputBus.Publish(message);
                _fsm.Handle(new SystemMessage.BecomeClone(_stateCorrelationId, _master));
            }
        }

        private bool IsLegitimateReplicationMessage(ReplicationMessage.IReplicationMessage message)
        {
            if (message.SubscriptionId == Guid.Empty) throw new Exception("IReplicationMessage with empty SubscriptionId provided.");
            if (message.SubscriptionId != _subscriptionId)
            {
                Log.Trace("Ignoring {0} because SubscriptionId {1:B} is wrong. Current SubscriptionId is {2:B}.",
                          message.GetType().Name, message.SubscriptionId, _subscriptionId);
                return false;
            }
            if (_master == null || _master.InstanceId != message.MasterId)
            {
                var msg = string.Format("{0} message passed SubscriptionId check, but master is either null or wrong. "
                                        + "Message.Master: [{1:B}], VNode Master: {2}.",
                                        message.GetType().Name, message.MasterId, _master);
                Log.Fatal(msg);
                Application.Exit(ExitCode.Error, msg);
                return false;
            }
            return true;
        }

        private void Handle(ClientMessage.RequestShutdown message)
        {
            _outputBus.Publish(message);
            _fsm.Handle(new SystemMessage.BecomeShuttingDown(Guid.NewGuid(), message.ExitProcess));
        }

        private void Handle(SystemMessage.ServiceShutdown message)
        {
            Log.Info("========== [{0}] Service '{1}' has shut down.", _nodeInfo.InternalHttp, message.ServiceName);

            _serviceShutdownsToExpect -= 1;
            if (_serviceShutdownsToExpect == 0)
            {
                Log.Info("========== [{0}] All Services Shutdown.", _nodeInfo.InternalHttp);
                Shutdown();
            }
            _outputBus.Publish(message);
        }

        private void Handle(SystemMessage.ShutdownTimeout message)
        {
            Debug.Assert(_state == VNodeState.ShuttingDown);

            Log.Error("========== [{0}] Shutdown Timeout.", _nodeInfo.InternalHttp);
            Shutdown();
            _outputBus.Publish(message);
        }

        private void Shutdown()
        {
            Debug.Assert(_state == VNodeState.ShuttingDown);

            _db.Close();
            _fsm.Handle(new SystemMessage.BecomeShutdown(_stateCorrelationId));
        }
    }
}
