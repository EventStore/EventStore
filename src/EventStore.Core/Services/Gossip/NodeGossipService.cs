using System;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Cluster;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.EpochManager;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.Services.TimerService;

namespace EventStore.Core.Services.Gossip {
	public class NodeGossipService : GossipServiceBase, IHandle<GossipMessage.UpdateNodePriority> {
		private readonly ICheckpoint _writerCheckpoint;
		private readonly ICheckpoint _chaserCheckpoint;
		private readonly IEpochManager _epochManager;
		private readonly Func<long> _getLastCommitPosition;
		private int _nodePriority;
		private readonly ITimeProvider _timeProvider;

		public NodeGossipService(IPublisher bus,
			IGossipSeedSource gossipSeedSource,
			VNodeInfo nodeInfo,
			ICheckpoint writerCheckpoint,
			ICheckpoint chaserCheckpoint,
			IEpochManager epochManager,
			Func<long> getLastCommitPosition,
			int nodePriority,
			TimeSpan gossipInterval,
			TimeSpan allowedTimeDifference,
			TimeSpan gossipTimeout,
			ITimeProvider timeProvider,
			Func<MemberInfo[], MemberInfo> getNodeToGossipTo = null)
			: base(bus, gossipSeedSource, nodeInfo, gossipInterval, allowedTimeDifference, gossipTimeout, timeProvider, getNodeToGossipTo) {
			Ensure.NotNull(writerCheckpoint, nameof(writerCheckpoint));
			Ensure.NotNull(chaserCheckpoint, nameof(chaserCheckpoint));
			Ensure.NotNull(epochManager, nameof(epochManager));
			Ensure.NotNull(getLastCommitPosition, nameof(getLastCommitPosition));

			_writerCheckpoint = writerCheckpoint;
			_chaserCheckpoint = chaserCheckpoint;
			_epochManager = epochManager;
			_getLastCommitPosition = getLastCommitPosition;
			_nodePriority = nodePriority;
			_timeProvider = timeProvider;
		}

		protected override MemberInfo GetInitialMe() {
			var lastEpoch = _epochManager.GetLastEpoch();
			var initialState = NodeInfo.IsReadOnlyReplica ? VNodeState.ReadOnlyLeaderless : VNodeState.Unknown;
			return MemberInfo.ForVNode(NodeInfo.InstanceId,
				_timeProvider.UtcNow,
				initialState,
				true,
				NodeInfo.InternalTcp,
				NodeInfo.InternalSecureTcp,
				NodeInfo.ExternalTcp,
				NodeInfo.ExternalSecureTcp,
				NodeInfo.InternalHttp,
				NodeInfo.ExternalHttp,
				_getLastCommitPosition(),
				_writerCheckpoint.Read(),
				_chaserCheckpoint.Read(),
				lastEpoch == null ? -1 : lastEpoch.EpochPosition,
				lastEpoch == null ? -1 : lastEpoch.EpochNumber,
				lastEpoch == null ? Guid.Empty : lastEpoch.EpochId,
				_nodePriority,
				NodeInfo.IsReadOnlyReplica);
		}

		protected override MemberInfo GetUpdatedMe(MemberInfo me) {
			return me.Updated(isAlive: true,
				state: CurrentRole,
				lastCommitPosition: _getLastCommitPosition(),
				writerCheckpoint: _writerCheckpoint.ReadNonFlushed(),
				chaserCheckpoint: _chaserCheckpoint.ReadNonFlushed(),
				epoch: _epochManager.GetLastEpoch(),
				nodePriority: _nodePriority,
				utcNow: _timeProvider.UtcNow);
		}

		public void Handle(GossipMessage.UpdateNodePriority message) {
			_nodePriority = message.NodePriority;
		}
	}
}
