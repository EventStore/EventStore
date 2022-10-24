using System;
using System.Net;
using EventStore.Core.Cluster;
using EventStore.Core.Messaging;

namespace EventStore.Core.Messages {
	public static partial class GossipMessage {
		[StatsGroup("gossip")]
		public enum MessageType {
			Non = 0,
			RetrieveGossipSeedSources = 1,
			GotGossipSeedSources = 2,
			Gossip = 3,
			GossipReceived = 4,
			ReadGossip = 5,
			SendGossip = 6,
			ClientGossip = 7,
			SendClientGossip = 8,
			GossipUpdated = 9,
			GossipSendFailed = 10,
			GetGossip = 11,
			GetGossipFailed = 12,
			GetGossipReceived = 13,
			UpdateNodePriority = 14,
		}

		[StatsMessage(MessageType.RetrieveGossipSeedSources)]
		public partial class RetrieveGossipSeedSources : Message {
		}

		[StatsMessage(MessageType.GotGossipSeedSources)]
		public partial class GotGossipSeedSources : Message {
			public readonly EndPoint[] GossipSeeds;

			public GotGossipSeedSources(EndPoint[] gossipSeeds) {
				GossipSeeds = gossipSeeds;
			}
		}

		[StatsMessage(MessageType.Gossip)]
		public partial class Gossip : Message {
			public readonly int GossipRound;

			public Gossip(int gossipRound) {
				GossipRound = gossipRound;
			}
		}

		[StatsMessage(MessageType.GossipReceived)]
		public partial class GossipReceived : Message {
			public readonly IEnvelope Envelope;
			public readonly ClusterInfo ClusterInfo;
			public readonly EndPoint Server;

			public GossipReceived(IEnvelope envelope, ClusterInfo clusterInfo, EndPoint server) {
				Envelope = envelope;
				ClusterInfo = clusterInfo;
				Server = server;
			}
		}

		[StatsMessage(MessageType.ReadGossip)]
		public partial class ReadGossip : Message {
			public readonly IEnvelope Envelope;

			public ReadGossip(IEnvelope envelope) {
				Envelope = envelope;
			}
		}

		[StatsMessage(MessageType.SendGossip)]
		public partial class SendGossip : Message {
			public readonly ClusterInfo ClusterInfo;
			public readonly EndPoint ServerEndPoint;

			public SendGossip(ClusterInfo clusterInfo, EndPoint serverEndPoint) {
				ClusterInfo = clusterInfo;
				ServerEndPoint = serverEndPoint;
			}
		}
		
		[StatsMessage(MessageType.ClientGossip)]
		public partial class ClientGossip : Message {
			public readonly IEnvelope Envelope;

			public ClientGossip(IEnvelope envelope) {
				Envelope = envelope;
			}
		}

		[StatsMessage(MessageType.SendClientGossip)]
		public partial class SendClientGossip : Message {
			public readonly ClientClusterInfo ClusterInfo;

			public SendClientGossip(ClientClusterInfo clusterInfo) {
				ClusterInfo = clusterInfo;
			}
		}

		[StatsMessage(MessageType.GossipUpdated)]
		public partial class GossipUpdated : Message {
			public readonly ClusterInfo ClusterInfo;

			public GossipUpdated(ClusterInfo clusterInfo) {
				ClusterInfo = clusterInfo;
			}
		}

		[StatsMessage(MessageType.GossipSendFailed)]
		public partial class GossipSendFailed : Message {
			public readonly string Reason;
			public readonly EndPoint Recipient;

			public GossipSendFailed(string reason, EndPoint recipient) {
				Reason = reason;
				Recipient = recipient;
			}

			public override string ToString() {
				return String.Format("Reason: {0}, Recipient: {1}", Reason, Recipient);
			}
		}
		
		[StatsMessage(MessageType.GetGossip)]
		public partial class GetGossip : Message {
			public GetGossip() { }
		}
		
		[StatsMessage(MessageType.GetGossipFailed)]
		public partial class GetGossipFailed : Message {
			public readonly string Reason;
			public readonly EndPoint Recipient;

			public GetGossipFailed(string reason, EndPoint recipient) {
				Reason = reason;
				Recipient = recipient;
			}

			public override string ToString() {
				return String.Format("Reason: {0}, Recipient: {1}", Reason, Recipient);
			}
		}
		
		[StatsMessage(MessageType.GetGossipReceived)]
		public partial class GetGossipReceived : Message {
			public readonly ClusterInfo ClusterInfo;
			public readonly EndPoint Server;

			public GetGossipReceived(ClusterInfo clusterInfo, EndPoint server) {
				ClusterInfo = clusterInfo;
				Server = server;
			}
		}

		[StatsMessage(MessageType.UpdateNodePriority)]
		public partial class UpdateNodePriority : Message {
			public readonly int NodePriority;

			public UpdateNodePriority(int priority) {
				NodePriority = priority;
			}
		}
	}
}
