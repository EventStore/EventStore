using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Cluster;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Storage.EpochManager;
using EventStore.Core.Services.TimerService;
using EventStore.Core.TransactionLog.Checkpoint;

namespace EventStore.Core.Services {
	public enum ElectionsState {
		Idle,
		ElectingLeader,
		Leader,
		Acceptor,
		Shutdown
	}

	public class ElectionsService : IHandle<SystemMessage.BecomeShuttingDown>,
		IHandle<GossipMessage.GossipUpdated>,
		IHandle<ElectionMessage.StartElections>,
		IHandle<ElectionMessage.ElectionsTimedOut>,
		IHandle<ElectionMessage.ViewChange>,
		IHandle<ElectionMessage.ViewChangeProof>,
		IHandle<ElectionMessage.SendViewChangeProof>,
		IHandle<ElectionMessage.Prepare>,
		IHandle<ElectionMessage.PrepareOk>,
		IHandle<ElectionMessage.Proposal>,
		IHandle<ElectionMessage.Accept>,
		IHandle<ClientMessage.SetNodePriority>,
		IHandle<ClientMessage.ResignNode>,
		IHandle<ElectionMessage.MasterIsResigning>,
		IHandle<ElectionMessage.MasterIsResigningOk> {
		public static readonly TimeSpan LeaderElectionProgressTimeout = TimeSpan.FromMilliseconds(1000);
		public static readonly TimeSpan SendViewChangeProofInterval = TimeSpan.FromMilliseconds(5000);

		private static readonly ILogger Log = LogManager.GetLoggerFor<ElectionsService>();
		private static readonly IPEndPointComparer IPComparer = new IPEndPointComparer();

		private readonly IPublisher _publisher;
		private readonly IEnvelope _publisherEnvelope;
		private readonly VNodeInfo _nodeInfo;
		private readonly int _clusterSize;
		private readonly ICheckpoint _writerCheckpoint;
		private readonly ICheckpoint _chaserCheckpoint;
		private readonly IEpochManager _epochManager;
		private readonly Func<long> _getLastCommitPosition;
		private int _nodePriority;
		private readonly ITimeProvider _timeProvider;

		private int _lastAttemptedView = -1;
		private int _lastInstalledView = -1;
		private ElectionsState _state = ElectionsState.Idle;

		private readonly HashSet<Guid> _vcReceived = new HashSet<Guid>();

		private readonly Dictionary<Guid, ElectionMessage.PrepareOk> _prepareOkReceived =
			new Dictionary<Guid, ElectionMessage.PrepareOk>();

		private readonly HashSet<Guid> _masterIsResigningOkReceived = new HashSet<Guid>();
		private readonly HashSet<Guid> _acceptsReceived = new HashSet<Guid>();

		private MasterCandidate _masterProposal;
		private Guid? _master;
		private Guid? _lastElectedMaster;

		private MemberInfo[] _servers;
		private Guid? _resigningMasterInstanceId;

		public ElectionsService(IPublisher publisher,
			VNodeInfo nodeInfo,
			int clusterSize,
			ICheckpoint writerCheckpoint,
			ICheckpoint chaserCheckpoint,
			IEpochManager epochManager,
			Func<long> getLastCommitPosition,
			int nodePriority,
			ITimeProvider timeProvider) {
			Ensure.NotNull(publisher, nameof(publisher));
			Ensure.NotNull(nodeInfo, nameof(nodeInfo));
			Ensure.Positive(clusterSize, nameof(clusterSize));
			Ensure.NotNull(writerCheckpoint, nameof(writerCheckpoint));
			Ensure.NotNull(chaserCheckpoint, nameof(chaserCheckpoint));
			Ensure.NotNull(epochManager, nameof(epochManager));
			Ensure.NotNull(getLastCommitPosition, nameof(getLastCommitPosition));
			Ensure.NotNull(timeProvider, nameof(timeProvider));

			_publisher = publisher;
			_nodeInfo = nodeInfo;
			_publisherEnvelope = new PublishEnvelope(_publisher);
			_clusterSize = clusterSize;
			_writerCheckpoint = writerCheckpoint;
			_chaserCheckpoint = chaserCheckpoint;
			_epochManager = epochManager;
			_getLastCommitPosition = getLastCommitPosition;
			_nodePriority = nodePriority;
			_timeProvider = timeProvider;

			var ownInfo = GetOwnInfo();
			_servers = new[] {
				MemberInfo.ForVNode(nodeInfo.InstanceId,
					_timeProvider.UtcNow,
					VNodeState.Initializing,
					true,
					nodeInfo.InternalTcp, nodeInfo.InternalSecureTcp,
					nodeInfo.ExternalTcp, nodeInfo.ExternalSecureTcp,
					nodeInfo.InternalHttp, nodeInfo.ExternalHttp,
					ownInfo.LastCommitPosition, ownInfo.WriterCheckpoint, ownInfo.ChaserCheckpoint,
					ownInfo.EpochPosition, ownInfo.EpochNumber, ownInfo.EpochId, ownInfo.NodePriority,
					nodeInfo.IsReadOnlyReplica)
			};
		}

		public void SubscribeMessages(ISubscriber subscriber) {
			subscriber.Subscribe<SystemMessage.BecomeShuttingDown>(this);
			subscriber.Subscribe<GossipMessage.GossipUpdated>(this);
			subscriber.Subscribe<ElectionMessage.StartElections>(this);
			subscriber.Subscribe<ElectionMessage.ElectionsTimedOut>(this);
			subscriber.Subscribe<ElectionMessage.ViewChange>(this);
			subscriber.Subscribe<ElectionMessage.ViewChangeProof>(this);
			subscriber.Subscribe<ElectionMessage.SendViewChangeProof>(this);
			subscriber.Subscribe<ElectionMessage.Prepare>(this);
			subscriber.Subscribe<ElectionMessage.PrepareOk>(this);
			subscriber.Subscribe<ElectionMessage.Proposal>(this);
			subscriber.Subscribe<ElectionMessage.Accept>(this);
			subscriber.Subscribe<ElectionMessage.MasterIsResigning>(this);
			subscriber.Subscribe<ElectionMessage.MasterIsResigningOk>(this);
			subscriber.Subscribe<ClientMessage.SetNodePriority>(this);
			subscriber.Subscribe<ClientMessage.ResignNode>(this);
		}

		public void Handle(ClientMessage.SetNodePriority message) {
			Log.Debug("Setting Node Priority to {nodePriority}.", message.NodePriority);
			_nodePriority = message.NodePriority;
			_publisher.Publish(new GossipMessage.UpdateNodePriority(_nodePriority));
		}

		public void Handle(ClientMessage.ResignNode message) {
			if (_master != null && _nodeInfo.InstanceId == _master) {
				_resigningMasterInstanceId = _master;
				var masterIsResigningMessageOk = new ElectionMessage.MasterIsResigningOk(
					_nodeInfo.InstanceId,
					_nodeInfo.InternalHttp,
					_nodeInfo.InstanceId,
					_nodeInfo.InternalHttp);
				Handle(masterIsResigningMessageOk);
				SendToAllExceptMe(new ElectionMessage.MasterIsResigning(
					_nodeInfo.InstanceId, _nodeInfo.InternalHttp));
			} else {
				Log.Info("ELECTIONS: ONLY MASTER RESIGNATION IS SUPPORTED AT THE MOMENT. IGNORING RESIGNATION.");
			}
		}

		public void Handle(ElectionMessage.MasterIsResigning message) {
			if (_nodeInfo.IsReadOnlyReplica) {
				Log.Debug(
					"ELECTIONS: THIS NODE IS A READ ONLY REPLICA. IT IS NOT ALLOWED TO VOTE AND THEREFOR NOT ALLOWED TO ACKNOWLEDGE MASTER RESIGNATION.");
				return;
			}
			Log.Debug("ELECTIONS: MASTER IS RESIGNING [{masterInternalHttp}, {masterId:B}].",
				message.MasterInternalHttp, message.MasterId);
			var masterIsResigningMessageOk = new ElectionMessage.MasterIsResigningOk(
				message.MasterId,
				message.MasterInternalHttp,
				_nodeInfo.InstanceId,
				_nodeInfo.InternalHttp);
			_resigningMasterInstanceId = message.MasterId;
			_publisher.Publish(new HttpMessage.SendOverHttp(message.MasterInternalHttp, masterIsResigningMessageOk,
				_timeProvider.LocalTime.Add(LeaderElectionProgressTimeout)));
		}

		public void Handle(ElectionMessage.MasterIsResigningOk message) {
			if (_masterIsResigningOkReceived.Add(message.ServerId) &&
			    _masterIsResigningOkReceived.Count == _clusterSize / 2 + 1) {
				_masterIsResigningOkReceived.Clear();
				Log.Debug(
					"ELECTIONS: MAJORITY OF ACCEPTANCE OF RESIGNATION OF MASTER [{masterInternalHttp}, {masterId:B}]. NOW INITIATING MASTER RESIGNATION.",
					message.MasterInternalHttp, message.MasterId);
				_publisher.Publish(new SystemMessage.InitiateMasterResignation());
			}
		}

		public void Handle(SystemMessage.BecomeShuttingDown message) {
			_state = ElectionsState.Shutdown;
		}

		public void Handle(GossipMessage.GossipUpdated message) {
			_servers = message.ClusterInfo.Members.Where(x => x.State != VNodeState.Manager)
				.Where(x => x.IsAlive)
				.OrderByDescending(x => x.InternalHttpEndPoint, IPComparer)
				.ToArray();
		}

		public void Handle(ElectionMessage.StartElections message) {
			if (_state == ElectionsState.Shutdown) return;
			if (_state == ElectionsState.ElectingLeader) return;

			if (_nodeInfo.IsReadOnlyReplica)
				Log.Trace("ELECTIONS: THIS NODE IS A READ ONLY REPLICA.");

			Log.Debug("ELECTIONS: STARTING ELECTIONS.");
			ShiftToLeaderElection(_lastAttemptedView + 1);
			_publisher.Publish(TimerMessage.Schedule.Create(SendViewChangeProofInterval,
				_publisherEnvelope,
				new ElectionMessage.SendViewChangeProof()));
		}

		public void Handle(ElectionMessage.ElectionsTimedOut message) {
			if (_state == ElectionsState.Shutdown) return;
			if (message.View != _lastAttemptedView) return;
			// we are still on the same view, but we selected master
			if (_state != ElectionsState.ElectingLeader && _master != null) return;

			Log.Debug("ELECTIONS: (V={view}) TIMED OUT! (S={state}, M={master}).", message.View, _state, _master);
			ShiftToLeaderElection(_lastAttemptedView + 1);
		}

		private void ShiftToLeaderElection(int view) {
			Log.Debug("ELECTIONS: (V={view}) SHIFT TO LEADER ELECTION.", view);

			_state = ElectionsState.ElectingLeader;
			_vcReceived.Clear();
			_prepareOkReceived.Clear();
			_lastAttemptedView = view;

			_masterProposal = null;
			_master = null;
			_acceptsReceived.Clear();

			var viewChangeMsg = new ElectionMessage.ViewChange(_nodeInfo.InstanceId, _nodeInfo.InternalHttp, view);
			Handle(viewChangeMsg);
			SendToAllExceptMe(viewChangeMsg);
			_publisher.Publish(TimerMessage.Schedule.Create(LeaderElectionProgressTimeout,
				_publisherEnvelope,
				new ElectionMessage.ElectionsTimedOut(view)));
		}

		private void SendToAllExceptMe(Message message) {
			foreach (var server in _servers.Where(x => x.InstanceId != _nodeInfo.InstanceId)) {
				_publisher.Publish(new HttpMessage.SendOverHttp(server.InternalHttpEndPoint, message,
					_timeProvider.LocalTime.Add(LeaderElectionProgressTimeout)));
			}
		}

		public void Handle(ElectionMessage.ViewChange message) {
			if (_state == ElectionsState.Shutdown) return;
			if (_state == ElectionsState.Idle) return;

			if (message.AttemptedView <= _lastInstalledView) return;

			Log.Debug("ELECTIONS: (V={view}) VIEWCHANGE FROM [{serverInternalHttp}, {serverId:B}].",
				message.AttemptedView, message.ServerInternalHttp, message.ServerId);

			if (message.AttemptedView > _lastAttemptedView)
				ShiftToLeaderElection(message.AttemptedView);

			if (_vcReceived.Add(message.ServerId) && _vcReceived.Count == _clusterSize / 2 + 1) {
				Log.Debug("ELECTIONS: (V={view}) MAJORITY OF VIEWCHANGE.", message.AttemptedView);
				if (AmILeaderOf(_lastAttemptedView))
					ShiftToPreparePhase();
			}
		}

		public void Handle(ElectionMessage.SendViewChangeProof message) {
			if (_state == ElectionsState.Shutdown) return;

			if (_lastInstalledView >= 0)
				SendToAllExceptMe(new ElectionMessage.ViewChangeProof(_nodeInfo.InstanceId, _nodeInfo.InternalHttp,
					_lastInstalledView));

			_publisher.Publish(TimerMessage.Schedule.Create(SendViewChangeProofInterval,
				_publisherEnvelope,
				new ElectionMessage.SendViewChangeProof()));
		}

		public void Handle(ElectionMessage.ViewChangeProof message) {
			if (_state == ElectionsState.Shutdown) return;
			if (_state == ElectionsState.Idle) return;
			if (message.InstalledView <= _lastInstalledView) return;

			_lastAttemptedView = message.InstalledView;

			_publisher.Publish(TimerMessage.Schedule.Create(LeaderElectionProgressTimeout,
				_publisherEnvelope,
				new ElectionMessage.ElectionsTimedOut(_lastAttemptedView)));

			if (AmILeaderOf(_lastAttemptedView)) {
				Log.Debug(
					"ELECTIONS: (IV={installedView}) VIEWCHANGEPROOF FROM [{serverInternalHttp}, {serverId:B}]. JUMPING TO LEADER STATE.",
					message.InstalledView, message.ServerInternalHttp, message.ServerId);

				ShiftToPreparePhase();
			} else {
				Log.Debug(
					"ELECTIONS: (IV={installedView}) VIEWCHANGEPROOF FROM [{serverInternalHttp}, {serverId:B}]. JUMPING TO NON-LEADER STATE.",
					message.InstalledView, message.ServerInternalHttp, message.ServerId);

				ShiftToAcceptor();
			}
		}

		private bool AmILeaderOf(int lastAttemptedView) {
			var serversExcludingNonPotentialLeaders = _servers.Where(x => !x.IsReadOnlyReplica).ToArray();
			var leader =
				serversExcludingNonPotentialLeaders[lastAttemptedView % serversExcludingNonPotentialLeaders.Length];
			return leader.InstanceId == _nodeInfo.InstanceId;
		}

		private void ShiftToPreparePhase() {
			Log.Debug("ELECTIONS: (V={lastAttemptedView}) SHIFT TO PREPARE PHASE.", _lastAttemptedView);

			_lastInstalledView = _lastAttemptedView;
			_prepareOkReceived.Clear();

			Handle(CreatePrepareOk(_lastInstalledView));
			SendToAllExceptMe(new ElectionMessage.Prepare(_nodeInfo.InstanceId, _nodeInfo.InternalHttp,
				_lastInstalledView));
		}

		public void Handle(ElectionMessage.Prepare message) {
			if (_state == ElectionsState.Shutdown) return;
			if (message.ServerId == _nodeInfo.InstanceId) return;
			if (message.View != _lastAttemptedView) return;
			if (_servers.All(x => x.InstanceId != message.ServerId)) return; // unknown instance

			Log.Debug("ELECTIONS: (V={lastAttemptedView}) PREPARE FROM [{serverInternalHttp}, {serverId:B}].",
				_lastAttemptedView, message.ServerInternalHttp, message.ServerId);

			if (_state == ElectionsState.ElectingLeader) // install the view
				ShiftToAcceptor();

			if (_nodeInfo.IsReadOnlyReplica) {
				Log.Info("ELECTIONS: READ ONLY REPLICA CAN'T BE A CANDIDATE [{0}]", message.ServerInternalHttp);
				return;
			}

			var prepareOk = CreatePrepareOk(message.View);
			_publisher.Publish(new HttpMessage.SendOverHttp(message.ServerInternalHttp, prepareOk,
				_timeProvider.LocalTime.Add(LeaderElectionProgressTimeout)));
		}

		private ElectionMessage.PrepareOk CreatePrepareOk(int view) {
			var ownInfo = GetOwnInfo();
			return new ElectionMessage.PrepareOk(view, ownInfo.InstanceId, ownInfo.InternalHttp,
				ownInfo.EpochNumber, ownInfo.EpochPosition, ownInfo.EpochId,
				ownInfo.LastCommitPosition, ownInfo.WriterCheckpoint, ownInfo.ChaserCheckpoint,
				ownInfo.NodePriority);
		}

		private void ShiftToAcceptor() {
			Log.Debug("ELECTIONS: (V={lastAttemptedView}) SHIFT TO REG_ACCEPTOR.", _lastAttemptedView);

			_state = ElectionsState.Acceptor;
			_lastInstalledView = _lastAttemptedView;
		}

		public void Handle(ElectionMessage.PrepareOk msg) {
			if (_state == ElectionsState.Shutdown) return;
			if (_state != ElectionsState.ElectingLeader) return;
			if (msg.View != _lastAttemptedView) return;

			Log.Debug("ELECTIONS: (V={view}) PREPARE_OK FROM {nodeInfo}.", msg.View,
				FormatNodeInfo(msg.ServerInternalHttp, msg.ServerId,
					msg.LastCommitPosition, msg.WriterCheckpoint, msg.ChaserCheckpoint,
					msg.EpochNumber, msg.EpochPosition, msg.EpochId, msg.NodePriority));

			if (!_prepareOkReceived.ContainsKey(msg.ServerId)) {
				_prepareOkReceived.Add(msg.ServerId, msg);
				if (_prepareOkReceived.Count == _clusterSize / 2 + 1)
					ShiftToLeader();
			}
		}

		private void ShiftToLeader() {
			if (_nodeInfo.IsReadOnlyReplica) {
				Log.Debug("ELECTIONS: (V={lastAttemptedView}) NOT SHIFTING TO REG_LEADER AS I'M READONLY.",
					_lastAttemptedView);
				return;
			}

			Log.Debug("ELECTIONS: (V={lastAttemptedView}) SHIFT TO REG_LEADER.", _lastAttemptedView);

			_state = ElectionsState.Leader;
			SendProposal();
		}

		private void SendProposal() {
			_acceptsReceived.Clear();
			_masterProposal = null;

			var master = GetBestMasterCandidate(_prepareOkReceived, _servers, _lastElectedMaster,
				_resigningMasterInstanceId);
			if (master == null) {
				Log.Trace("ELECTIONS: (V={lastAttemptedView}) NO MASTER CANDIDATE WHEN TRYING TO SEND PROPOSAL.",
					_lastAttemptedView);
				return;
			}

			_masterProposal = master;

			Log.Debug("ELECTIONS: (V={lastAttemptedView}) SENDING PROPOSAL CANDIDATE: {formatNodeInfo}, ME: {ownInfo}.",
				_lastAttemptedView, FormatNodeInfo(master), FormatNodeInfo(GetOwnInfo()));

			var proposal = new ElectionMessage.Proposal(_nodeInfo.InstanceId, _nodeInfo.InternalHttp,
				master.InstanceId, master.InternalHttp,
				_lastInstalledView,
				master.EpochNumber, master.EpochPosition, master.EpochId,
				master.LastCommitPosition, master.WriterCheckpoint, master.ChaserCheckpoint, master.NodePriority);
			Handle(new ElectionMessage.Accept(_nodeInfo.InstanceId, _nodeInfo.InternalHttp,
				master.InstanceId, master.InternalHttp, _lastInstalledView));
			SendToAllExceptMe(proposal);
		}

		public static MasterCandidate GetBestMasterCandidate(Dictionary<Guid, ElectionMessage.PrepareOk> received,
			MemberInfo[] servers, Guid? lastElectedMaster, Guid? resigningMasterInstanceId) {
			if (lastElectedMaster.HasValue && lastElectedMaster.Value != resigningMasterInstanceId) {
				if (received.TryGetValue(lastElectedMaster.Value, out var masterMsg)) {
					return new MasterCandidate(masterMsg.ServerId, masterMsg.ServerInternalHttp,
						masterMsg.EpochNumber, masterMsg.EpochPosition, masterMsg.EpochId,
						masterMsg.LastCommitPosition, masterMsg.WriterCheckpoint, masterMsg.ChaserCheckpoint,
						masterMsg.NodePriority);
				}

				var master = servers.FirstOrDefault(x =>
					x.IsAlive && x.InstanceId == lastElectedMaster && x.State == VNodeState.Master);
				if (master != null) {
					return new MasterCandidate(master.InstanceId, master.InternalHttpEndPoint,
						master.EpochNumber, master.EpochPosition, master.EpochId,
						master.LastCommitPosition, master.WriterCheckpoint, master.ChaserCheckpoint,
						master.NodePriority);
				}
			}

			var best = received.Values
				.OrderByDescending(x => x.EpochNumber)
				.ThenByDescending(x => x.LastCommitPosition)
				.ThenByDescending(x => x.WriterCheckpoint)
				.ThenByDescending(x => x.ChaserCheckpoint)
				.ThenByDescending(x => x.NodePriority)
				.ThenByDescending(x => x.ServerId)
				.FirstOrDefault();
			if (best == null)
				return null;
			return new MasterCandidate(best.ServerId, best.ServerInternalHttp,
				best.EpochNumber, best.EpochPosition, best.EpochId,
				best.LastCommitPosition, best.WriterCheckpoint, best.ChaserCheckpoint, best.NodePriority);
		}

		public static bool IsLegitimateMaster(int view, IPEndPoint proposingServerEndPoint, Guid proposingServerId,
			MasterCandidate candidate, MemberInfo[] servers, Guid? lastElectedMaster, VNodeInfo nodeInfo,
			MasterCandidate ownInfo,
			Guid? resigningMaster) {
			var master = servers.FirstOrDefault(x =>
				x.IsAlive && x.InstanceId == lastElectedMaster && x.State == VNodeState.Master);

			if (master != null && master.InstanceId != resigningMaster) {
				if (candidate.InstanceId == master.InstanceId
				    || candidate.EpochNumber > master.EpochNumber
				    || (candidate.EpochNumber == master.EpochNumber && candidate.EpochId != master.EpochId))
					return true;

				Log.Debug(
					"ELECTIONS: (V={view}) NOT LEGITIMATE MASTER PROPOSAL FROM [{proposingServerEndPoint},{proposingServerId:B}] M={candidateInfo}. "
					+ "PREVIOUS MASTER IS ALIVE: [{masterInternalHttp},{masterId:B}].",
					view, proposingServerEndPoint, proposingServerId, FormatNodeInfo(candidate),
					master.InternalHttpEndPoint, master.InstanceId);
				return false;
			}

			if (candidate.InstanceId == nodeInfo.InstanceId)
				return true;

			if (!IsCandidateGoodEnough(candidate, ownInfo)) {
				Log.Debug(
					"ELECTIONS: (V={view}) NOT LEGITIMATE MASTER PROPOSAL FROM [{proposingServerEndPoint},{proposingServerId:B}] M={candidateInfo}. ME={ownInfo}.",
					view, proposingServerEndPoint, proposingServerId, FormatNodeInfo(candidate),
					FormatNodeInfo(ownInfo));
				return false;
			}

			return true;
		}

		private static bool IsCandidateGoodEnough(MasterCandidate candidate, MasterCandidate ownInfo) {
			if (candidate.EpochNumber != ownInfo.EpochNumber)
				return candidate.EpochNumber > ownInfo.EpochNumber;
			if (candidate.LastCommitPosition != ownInfo.LastCommitPosition)
				return candidate.LastCommitPosition > ownInfo.LastCommitPosition;
			if (candidate.WriterCheckpoint != ownInfo.WriterCheckpoint)
				return candidate.WriterCheckpoint > ownInfo.WriterCheckpoint;
			if (candidate.ChaserCheckpoint != ownInfo.ChaserCheckpoint)
				return candidate.ChaserCheckpoint > ownInfo.ChaserCheckpoint;
			return true;
		}

		public void Handle(ElectionMessage.Proposal message) {
			if (_state == ElectionsState.Shutdown) return;
			if (message.ServerId == _nodeInfo.InstanceId) return;
			if (_state != ElectionsState.Acceptor) return;
			if (message.View != _lastInstalledView) return;
			if (_servers.All(x => x.InstanceId != message.ServerId)) return;
			if (_servers.All(x => x.InstanceId != message.MasterId)) return;

			var candidate = new MasterCandidate(message.MasterId, message.MasterInternalHttp,
				message.EpochNumber, message.EpochPosition, message.EpochId,
				message.LastCommitPosition, message.WriterCheckpoint, message.ChaserCheckpoint, message.NodePriority);

			var ownInfo = GetOwnInfo();
			if (!IsLegitimateMaster(message.View, message.ServerInternalHttp, message.ServerId,
				candidate, _servers, _lastElectedMaster, _nodeInfo, ownInfo,
				_resigningMasterInstanceId))
				return;

			Log.Debug(
				"ELECTIONS: (V={lastAttemptedView}) PROPOSAL FROM [{serverInternalHttp},{serverId:B}] M={candidateInfo}. ME={ownInfo}, NodePriority={priority}",
				_lastAttemptedView,
				message.ServerInternalHttp, message.ServerId, FormatNodeInfo(candidate), FormatNodeInfo(GetOwnInfo()),
				message.NodePriority);

			if (_masterProposal == null) {
				_masterProposal = candidate;
				_acceptsReceived.Clear();
			}

			if (_masterProposal.InstanceId == message.MasterId) {
				// NOTE: proposal from other server is also implicit Accept from that server
				Handle(new ElectionMessage.Accept(message.ServerId, message.ServerInternalHttp,
					message.MasterId, message.MasterInternalHttp, message.View));
				var accept = new ElectionMessage.Accept(_nodeInfo.InstanceId, _nodeInfo.InternalHttp,
					message.MasterId, message.MasterInternalHttp, message.View);
				Handle(accept); // implicitly sent accept to ourselves
				SendToAllExceptMe(accept);
			}
		}

		public void Handle(ElectionMessage.Accept message) {
			if (_state == ElectionsState.Shutdown) return;
			if (message.View != _lastInstalledView) return;
			if (_masterProposal == null) return;
			if (_masterProposal.InstanceId != message.MasterId) return;

			Log.Debug(
				"ELECTIONS: (V={view}) ACCEPT FROM [{serverInternalHttp},{serverId:B}] M=[{masterInternalHttp},{masterId:B}]).",
				message.View,
				message.ServerInternalHttp, message.ServerId, message.MasterInternalHttp, message.MasterId);

			if (_acceptsReceived.Add(message.ServerId) && _acceptsReceived.Count == _clusterSize / 2 + 1) {
				var master = _servers.FirstOrDefault(x => x.InstanceId == _masterProposal.InstanceId);
				if (master != null) {
					_master = _masterProposal.InstanceId;
					Log.Info("ELECTIONS: (V={view}) DONE. ELECTED MASTER = {masterInfo}. ME={ownInfo}.", message.View,
						FormatNodeInfo(_masterProposal), FormatNodeInfo(GetOwnInfo()));
					_lastElectedMaster = _master;
					_resigningMasterInstanceId = null;
					_publisher.Publish(new ElectionMessage.ElectionsDone(message.View, master));
				}
			}
		}

		private MasterCandidate GetOwnInfo() {
			var lastEpoch = _epochManager.GetLastEpoch();
			var writerCheckpoint = _writerCheckpoint.Read();
			var chaserCheckpoint = _chaserCheckpoint.Read();
			var lastCommitPosition = _getLastCommitPosition();
			return new MasterCandidate(_nodeInfo.InstanceId, _nodeInfo.InternalHttp,
				lastEpoch == null ? -1 : lastEpoch.EpochNumber,
				lastEpoch == null ? -1 : lastEpoch.EpochPosition,
				lastEpoch == null ? Guid.Empty : lastEpoch.EpochId,
				lastCommitPosition, writerCheckpoint, chaserCheckpoint, _nodePriority);
		}

		private static string FormatNodeInfo(MasterCandidate candidate) {
			return FormatNodeInfo(candidate.InternalHttp, candidate.InstanceId,
				candidate.LastCommitPosition, candidate.WriterCheckpoint, candidate.ChaserCheckpoint,
				candidate.EpochNumber, candidate.EpochPosition, candidate.EpochId, candidate.NodePriority);
		}

		private static string FormatNodeInfo(IPEndPoint serverEndPoint, Guid serverId,
			long lastCommitPosition, long writerCheckpoint, long chaserCheckpoint,
			int epochNumber, long epochPosition, Guid epochId, int priority) {
			return string.Format("[{0},{1:B}](L={2},W={3},C={4},E{5}@{6}:{7:B},Priority={8})",
				serverEndPoint, serverId,
				lastCommitPosition, writerCheckpoint, chaserCheckpoint,
				epochNumber, epochPosition, epochId, priority);
		}

		public class MasterCandidate {
			public readonly Guid InstanceId;
			public readonly IPEndPoint InternalHttp;

			public readonly int EpochNumber;
			public readonly long EpochPosition;
			public readonly Guid EpochId;

			public readonly long LastCommitPosition;
			public readonly long WriterCheckpoint;
			public readonly long ChaserCheckpoint;

			public readonly int NodePriority;

			public MasterCandidate(Guid instanceId, IPEndPoint internalHttp,
				int epochNumber, long epochPosition, Guid epochId,
				long lastCommitPosition, long writerCheckpoint, long chaserCheckpoint,
				int nodePriority) {
				InstanceId = instanceId;
				InternalHttp = internalHttp;
				EpochNumber = epochNumber;
				EpochPosition = epochPosition;
				EpochId = epochId;
				LastCommitPosition = lastCommitPosition;
				WriterCheckpoint = writerCheckpoint;
				ChaserCheckpoint = chaserCheckpoint;
				NodePriority = nodePriority;
			}
		}
	}
}
