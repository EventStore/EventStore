using System;
using EventStore.BufferManagement;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Cluster;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.EpochManager;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Services.Gossip {
	public class NodeGossipService : GossipServiceBase, IHandle<GossipMessage.UpdateNodePriority> {
		private readonly ICheckpoint _writerCheckpoint;
		private readonly ICheckpoint _chaserCheckpoint;
		private readonly IEpochManager _epochManager;
		private readonly Func<long> _getLastCommitPosition;
		private int _nodePriority;
		private readonly Func<DateTime> _getUtcNow;
		private readonly Func<DateTime> _getNow;

		private static readonly ILogger Log = LogManager.GetLoggerFor<BufferManager>();

		public NodeGossipService(IPublisher bus,
			IGossipSeedSource gossipSeedSource,
			VNodeInfo nodeInfo,
			ICheckpoint writerCheckpoint,
			ICheckpoint chaserCheckpoint,
			IEpochManager epochManager,
			Func<long> getLastCommitPosition,
			int nodePriority,
			TimeSpan interval,
			TimeSpan allowedTimeDifference,
			Func<DateTime> getUtcNow = null,
			Func<DateTime> getNow = null,
			Func<MemberInfo[], MemberInfo> getNodeToGossipTo = null)
			: base(bus, gossipSeedSource, nodeInfo, interval, allowedTimeDifference, getUtcNow, getNow,
				getNodeToGossipTo) {
			Ensure.NotNull(writerCheckpoint, nameof(writerCheckpoint));
			Ensure.NotNull(chaserCheckpoint, nameof(chaserCheckpoint));
			Ensure.NotNull(epochManager, nameof(epochManager));
			Ensure.NotNull(getLastCommitPosition, nameof(getLastCommitPosition));

			_writerCheckpoint = writerCheckpoint;
			_chaserCheckpoint = chaserCheckpoint;
			_epochManager = epochManager;
			_getLastCommitPosition = getLastCommitPosition;
			_nodePriority = nodePriority;
			_getUtcNow = getUtcNow;
			_getNow = getNow;
		}

		protected override MemberInfo GetInitialMe() {
			var lastEpoch = _epochManager.GetLastEpoch();
			var initialState = NodeInfo.IsReadOnlyReplica ? VNodeState.ReadOnlyMasterless : VNodeState.Unknown;
			return MemberInfo.ForVNode(NodeInfo.InstanceId,
				_getUtcNow(),
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
				getUtcNow: _getUtcNow);
		}

		public void Handle(GossipMessage.UpdateNodePriority message) {
			_nodePriority = message.NodePriority;
		}
	}
}
