using System;
using System.Net;
using EventStore.Core.Cluster;
using EventStore.Core.Messaging;

namespace EventStore.Core.Messages {
	public static partial class GossipMessage {
		[DerivedMessage(CoreMessage.Gossip)]
		public partial class RetrieveGossipSeedSources : Message<RetrieveGossipSeedSources> {
		}

		[DerivedMessage(CoreMessage.Gossip)]
		public partial class GotGossipSeedSources : Message<GotGossipSeedSources> {
			public readonly EndPoint[] GossipSeeds;

			public GotGossipSeedSources(EndPoint[] gossipSeeds) {
				GossipSeeds = gossipSeeds;
			}
		}

		[DerivedMessage(CoreMessage.Gossip)]
		public partial class Gossip : Message<Gossip> {
			public readonly int GossipRound;

			public Gossip(int gossipRound) {
				GossipRound = gossipRound;
			}
		}

		[DerivedMessage(CoreMessage.Gossip)]
		public partial class GossipReceived : Message<GossipReceived> {
			public readonly IEnvelope Envelope;
			public readonly ClusterInfo ClusterInfo;
			public readonly EndPoint Server;

			public GossipReceived(IEnvelope envelope, ClusterInfo clusterInfo, EndPoint server) {
				Envelope = envelope;
				ClusterInfo = clusterInfo;
				Server = server;
			}
		}

		[DerivedMessage(CoreMessage.Gossip)]
		public partial class ReadGossip : Message<ReadGossip> {
			public readonly IEnvelope Envelope;

			public ReadGossip(IEnvelope envelope) {
				Envelope = envelope;
			}
		}

		[DerivedMessage(CoreMessage.Gossip)]
		public partial class SendGossip : Message<SendGossip> {
			public readonly ClusterInfo ClusterInfo;
			public readonly EndPoint ServerEndPoint;

			public SendGossip(ClusterInfo clusterInfo, EndPoint serverEndPoint) {
				ClusterInfo = clusterInfo;
				ServerEndPoint = serverEndPoint;
			}
		}
		
		[DerivedMessage(CoreMessage.Gossip)]
		public partial class ClientGossip : Message<ClientGossip> {
			public readonly IEnvelope Envelope;

			public ClientGossip(IEnvelope envelope) {
				Envelope = envelope;
			}
		}

		[DerivedMessage(CoreMessage.Gossip)]
		public partial class SendClientGossip : Message<SendClientGossip> {
			public readonly ClientClusterInfo ClusterInfo;

			public SendClientGossip(ClientClusterInfo clusterInfo) {
				ClusterInfo = clusterInfo;
			}
		}

		[DerivedMessage(CoreMessage.Gossip)]
		public partial class GossipUpdated : Message<GossipUpdated> {
			public readonly ClusterInfo ClusterInfo;

			public GossipUpdated(ClusterInfo clusterInfo) {
				ClusterInfo = clusterInfo;
			}
		}

		[DerivedMessage(CoreMessage.Gossip)]
		public partial class GossipSendFailed : Message<GossipSendFailed> {
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
		
		[DerivedMessage(CoreMessage.Gossip)]
		public partial class GetGossip : Message<GetGossip> {
			public GetGossip() { }
		}
		
		[DerivedMessage(CoreMessage.Gossip)]
		public partial class GetGossipFailed : Message<GetGossipFailed> {
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
		
		[DerivedMessage(CoreMessage.Gossip)]
		public partial class GetGossipReceived : Message<GetGossipReceived> {
			public readonly ClusterInfo ClusterInfo;
			public readonly EndPoint Server;

			public GetGossipReceived(ClusterInfo clusterInfo, EndPoint server) {
				ClusterInfo = clusterInfo;
				Server = server;
			}
		}

		[DerivedMessage(CoreMessage.Gossip)]
		public partial class UpdateNodePriority : Message<UpdateNodePriority> {
			public readonly int NodePriority;

			public UpdateNodePriority(int priority) {
				NodePriority = priority;
			}
		}
	}
}
