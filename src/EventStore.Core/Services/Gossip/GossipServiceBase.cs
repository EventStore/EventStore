using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Cluster;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;

namespace EventStore.Core.Services.Gossip {
	public abstract class GossipServiceBase : IHandle<SystemMessage.SystemInit>,
		IHandle<GossipMessage.RetrieveGossipSeedSources>,
		IHandle<GossipMessage.GotGossipSeedSources>,
		IHandle<GossipMessage.Gossip>,
		IHandle<GossipMessage.GossipReceived>,
		IHandle<SystemMessage.StateChangeMessage>,
		IHandle<GossipMessage.GossipSendFailed>,
		IHandle<SystemMessage.VNodeConnectionLost>,
		IHandle<SystemMessage.VNodeConnectionEstablished> {
		private static readonly TimeSpan DnsRetryTimeout = TimeSpan.FromMilliseconds(1000);
		private static readonly TimeSpan GossipStartupInterval = TimeSpan.FromMilliseconds(100);
		private static readonly TimeSpan DeadMemberRemovalTimeout = TimeSpan.FromMinutes(30);

		private static readonly ILogger Log = LogManager.GetLoggerFor<GossipServiceBase>();

		protected readonly VNodeInfo NodeInfo;
		protected VNodeState CurrentRole = VNodeState.Initializing;
		protected VNodeInfo CurrentMaster;
		private readonly TimeSpan GossipInterval = TimeSpan.FromMilliseconds(1000);
		private readonly TimeSpan AllowedTimeDifference = TimeSpan.FromMinutes(30);

		private readonly IPublisher _bus;
		private readonly IEnvelope _publishEnvelope;
		private readonly IGossipSeedSource _gossipSeedSource;

		private GossipState _state;
		private ClusterInfo _cluster;
		private readonly Random _rnd = new Random(Math.Abs(Environment.TickCount));

		protected GossipServiceBase(IPublisher bus,
			IGossipSeedSource gossipSeedSource,
			VNodeInfo nodeInfo,
			TimeSpan gossipInterval,
			TimeSpan allowedTimeDifference) {
			Ensure.NotNull(bus, "bus");
			Ensure.NotNull(gossipSeedSource, "gossipSeedSource");
			Ensure.NotNull(nodeInfo, "nodeInfo");

			_bus = bus;
			_publishEnvelope = new PublishEnvelope(bus);
			_gossipSeedSource = gossipSeedSource;
			NodeInfo = nodeInfo;
			GossipInterval = gossipInterval;
			AllowedTimeDifference = allowedTimeDifference;
			_state = GossipState.Startup;
		}

		protected abstract MemberInfo GetInitialMe();
		protected abstract MemberInfo GetUpdatedMe(MemberInfo me);

		public void Handle(SystemMessage.SystemInit message) {
			if (_state != GossipState.Startup)
				return;
			_cluster = new ClusterInfo(GetInitialMe());
			Handle(new GossipMessage.RetrieveGossipSeedSources());
		}

		public void Handle(GossipMessage.RetrieveGossipSeedSources message) {
			_state = GossipState.RetrievingGossipSeeds;
			try {
				_gossipSeedSource.BeginGetHostEndpoints(OnGotGossipSeedSources, null);
			} catch (Exception ex) {
				Log.ErrorException(ex, "Error while retrieving cluster members through DNS.");
				_bus.Publish(TimerMessage.Schedule.Create(DnsRetryTimeout, _publishEnvelope,
					new GossipMessage.RetrieveGossipSeedSources()));
			}
		}

		private void OnGotGossipSeedSources(IAsyncResult ar) {
			try {
				var entries = _gossipSeedSource.EndGetHostEndpoints(ar);
				_bus.Publish(new GossipMessage.GotGossipSeedSources(entries));
			} catch (Exception ex) {
				Log.ErrorException(ex, "Error while retrieving cluster members through DNS.");
				_bus.Publish(TimerMessage.Schedule.Create(DnsRetryTimeout, _publishEnvelope,
					new GossipMessage.RetrieveGossipSeedSources()));
			}
		}

		public void Handle(GossipMessage.GotGossipSeedSources message) {
			var now = DateTime.UtcNow;
			var dnsCluster = new ClusterInfo(
				message.GossipSeeds.Select(x => MemberInfo.ForManager(Guid.Empty, now, true, x, x)).ToArray());

			var oldCluster = _cluster;
			_cluster = MergeClusters(_cluster, dnsCluster, null, x => x);
			LogClusterChange(oldCluster, _cluster, null);

			_state = GossipState.Working;
			Handle(new GossipMessage.Gossip(0)); // start gossiping
		}

		public void Handle(GossipMessage.Gossip message) {
			if (_state != GossipState.Working)
				return;

			var node = GetNodeToGossipTo(_cluster.Members);
			if (node != null) {
				_cluster = UpdateCluster(_cluster, x => x.InstanceId == NodeInfo.InstanceId ? GetUpdatedMe(x) : x);
				_bus.Publish(new HttpMessage.SendOverHttp(node.InternalHttpEndPoint,
					new GossipMessage.SendGossip(_cluster, NodeInfo.InternalHttp), DateTime.Now.Add(GossipInterval)));
			}

			var interval = message.GossipRound < 20 ? GossipStartupInterval : GossipInterval;
			var gossipRound = Math.Min(2000000000, node == null ? message.GossipRound : message.GossipRound + 1);
			_bus.Publish(
				TimerMessage.Schedule.Create(interval, _publishEnvelope, new GossipMessage.Gossip(gossipRound)));
		}

		private MemberInfo GetNodeToGossipTo(MemberInfo[] members) {
			if (members.Length == 0)
				return null;
			for (int i = 0; i < 5; ++i) {
				var node = members[_rnd.Next(members.Length)];
				if (node.InstanceId != NodeInfo.InstanceId)
					return node;
			}

			return null;
		}

		public void Handle(GossipMessage.GossipReceived message) {
			if (_state != GossipState.Working)
				return;

			var oldCluster = _cluster;
			_cluster = MergeClusters(_cluster,
				message.ClusterInfo,
				message.Server,
				x => x.InstanceId == NodeInfo.InstanceId ? GetUpdatedMe(x) : x);

			message.Envelope.ReplyWith(new GossipMessage.SendGossip(_cluster, NodeInfo.InternalHttp));

			if (_cluster.HasChangedSince(oldCluster))
				LogClusterChange(oldCluster, _cluster, string.Format("gossip received from [{0}]", message.Server));
			_bus.Publish(new GossipMessage.GossipUpdated(_cluster));
		}

		public void Handle(SystemMessage.StateChangeMessage message) {
			CurrentRole = message.State;
			var replicaState = message as SystemMessage.ReplicaStateMessage;
			CurrentMaster = replicaState == null ? null : replicaState.Master;
			_cluster = UpdateCluster(_cluster, x => x.InstanceId == NodeInfo.InstanceId ? GetUpdatedMe(x) : x);

			//if (_cluster.HasChangedSince(oldCluster))
			//LogClusterChange(oldCluster, _cluster, _nodeInfo.InternalHttp);
			_bus.Publish(new GossipMessage.GossipUpdated(_cluster));
		}

		public void Handle(GossipMessage.GossipSendFailed message) {
			var node = _cluster.Members.FirstOrDefault(x => x.Is(message.Recipient));
			if (node == null || !node.IsAlive)
				return;

			if (CurrentMaster != null && node.InstanceId == CurrentMaster.InstanceId) {
				Log.Trace(
					"Looks like master [{masterEndPoint}, {instanceId:B}] is DEAD (Gossip send failed), though we wait for TCP to decide.",
					message.Recipient, node.InstanceId);
				return;
			}

			Log.Trace("Looks like node [{nodeEndPoint}] is DEAD (Gossip send failed).", message.Recipient);

			var oldCluster = _cluster;
			_cluster = UpdateCluster(_cluster, x => x.Is(message.Recipient) ? x.Updated(isAlive: false) : x);
			if (_cluster.HasChangedSince(oldCluster))
				LogClusterChange(oldCluster, _cluster, string.Format("gossip send failed to [{0}]", message.Recipient));
			_bus.Publish(new GossipMessage.GossipUpdated(_cluster));
		}

		public void Handle(SystemMessage.VNodeConnectionLost message) {
			var node = _cluster.Members.FirstOrDefault(x => x.Is(message.VNodeEndPoint));
			if (node == null || !node.IsAlive)
				return;

			Log.Trace("Looks like node [{nodeEndPoint}] is DEAD (TCP connection lost).", message.VNodeEndPoint);

			var oldCluster = _cluster;
			_cluster = UpdateCluster(_cluster, x => x.Is(message.VNodeEndPoint) ? x.Updated(isAlive: false) : x);
			if (_cluster.HasChangedSince(oldCluster))
				LogClusterChange(oldCluster, _cluster,
					string.Format("TCP connection lost to [{0}]", message.VNodeEndPoint));
			_bus.Publish(new GossipMessage.GossipUpdated(_cluster));
		}

		public void Handle(SystemMessage.VNodeConnectionEstablished message) {
			var oldCluster = _cluster;
			_cluster = UpdateCluster(_cluster, x => x.Is(message.VNodeEndPoint) ? x.Updated(isAlive: true) : x);
			if (_cluster.HasChangedSince(oldCluster))
				LogClusterChange(oldCluster, _cluster,
					string.Format("TCP connection established to [{0}]", message.VNodeEndPoint));
			_bus.Publish(new GossipMessage.GossipUpdated(_cluster));
		}

		private ClusterInfo MergeClusters(ClusterInfo myCluster, ClusterInfo othersCluster,
			IPEndPoint peerEndPoint, Func<MemberInfo, MemberInfo> update) {
			var mems = myCluster.Members.ToDictionary(member => member.InternalHttpEndPoint);
			foreach (var member in othersCluster.Members) {
				if (member.InstanceId == NodeInfo.InstanceId || member.Is(NodeInfo.InternalHttp)
				) // we know about ourselves better
					continue;
				if (peerEndPoint != null && member.Is(peerEndPoint)) // peer knows about itself better
				{
					if ((DateTime.UtcNow - member.TimeStamp).Duration() > AllowedTimeDifference) {
						Log.Error("Time difference between us and [{peerEndPoint}] is too great! "
						          + "UTC now: {dateTime:yyyy-MM-dd HH:mm:ss.fff}, peer's time stamp: {peerTimestamp:yyyy-MM-dd HH:mm:ss.fff}.",
							peerEndPoint, DateTime.UtcNow, member.TimeStamp);
					}

					mems[member.InternalHttpEndPoint] = member;
				} else {
					MemberInfo existingMem;
					// if there is no data about this member or data is stale -- update
					if (!mems.TryGetValue(member.InternalHttpEndPoint, out existingMem) ||
					    IsMoreUpToDate(member, existingMem)) {
						// we do not trust master's alive status and state to come from outside
						if (CurrentMaster != null && existingMem != null &&
						    member.InstanceId == CurrentMaster.InstanceId)
							mems[member.InternalHttpEndPoint] =
								member.Updated(isAlive: existingMem.IsAlive, state: existingMem.State);
						else
							mems[member.InternalHttpEndPoint] = member;
					}
				}
			}

			// update members and remove dead timed-out members, if there are any
			var newMembers = mems.Values.Select(update)
				.Where(x => x.IsAlive || DateTime.UtcNow - x.TimeStamp < DeadMemberRemovalTimeout);
			return new ClusterInfo(newMembers);
		}

		private static bool IsMoreUpToDate(MemberInfo member, MemberInfo existingMem) {
			if (member.EpochNumber != existingMem.EpochNumber)
				return member.EpochNumber > existingMem.EpochNumber;
			if (member.WriterCheckpoint != existingMem.WriterCheckpoint)
				return member.WriterCheckpoint > existingMem.WriterCheckpoint;
			return member.TimeStamp > existingMem.TimeStamp;
		}

		private static ClusterInfo UpdateCluster(ClusterInfo cluster, Func<MemberInfo, MemberInfo> update) {
			// update members and remove dead timed-out members, if there are any
			var newMembers = cluster.Members.Select(update)
				.Where(x => x.IsAlive || DateTime.UtcNow - x.TimeStamp < DeadMemberRemovalTimeout);
			return new ClusterInfo(newMembers);
		}

		private static void LogClusterChange(ClusterInfo oldCluster, ClusterInfo newCluster, string source) {
			var ipEndPointComparer = new IPEndPointComparer();

			if (!LogManager.StructuredLog) {
				Log.Trace("CLUSTER HAS CHANGED{0}", source.IsNotEmptyString() ? " (" + source + ")" : string.Empty);
				Log.Trace("Old:");
				foreach (var oldMember in oldCluster.Members.OrderByDescending(x => x.InternalHttpEndPoint,
					ipEndPointComparer)) {
					Log.Trace(oldMember.ToString());
				}

				Log.Trace("New:");
				foreach (var newMember in newCluster.Members.OrderByDescending(x => x.InternalHttpEndPoint,
					ipEndPointComparer)) {
					Log.Trace(newMember.ToString());
				}

				Log.Trace(new string('-', 80));
			} else {
				List<MemberInfo> oldMembers = oldCluster.Members
					.OrderByDescending(x => x.InternalHttpEndPoint, ipEndPointComparer).ToList();
				List<MemberInfo> newMembers = newCluster.Members
					.OrderByDescending(x => x.InternalHttpEndPoint, ipEndPointComparer).ToList();
				Log.Trace(
					"CLUSTER HAS CHANGED {source}"
					+ "\nOld:"
					+ "\n{@oldMembers}"
					+ "\nNew:"
					+ "\n{@newMembers}"
					, source.IsNotEmptyString() ? source : string.Empty
					, oldMembers
					, newMembers
				);
			}
		}
	}
}
