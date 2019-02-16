using System;
using System.Net;
using System.Threading;
using EventStore.Core.Cluster;
using EventStore.Core.Messaging;

namespace EventStore.Core.Messages {
	public static partial class GossipMessage {
		public class RetrieveGossipSeedSources : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}
		}

		public class GotGossipSeedSources : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly IPEndPoint[] GossipSeeds;

			public GotGossipSeedSources(IPEndPoint[] gossipSeeds) {
				GossipSeeds = gossipSeeds;
			}
		}

		public class Gossip : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly int GossipRound;

			public Gossip(int gossipRound) {
				GossipRound = gossipRound;
			}
		}

		public class GossipReceived : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly IEnvelope Envelope;
			public readonly ClusterInfo ClusterInfo;
			public readonly IPEndPoint Server;

			public GossipReceived(IEnvelope envelope, ClusterInfo clusterInfo, IPEndPoint server) {
				Envelope = envelope;
				ClusterInfo = clusterInfo;
				Server = server;
			}
		}

		public class SendGossip : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly ClusterInfo ClusterInfo;
			public readonly IPEndPoint ServerEndPoint;

			public SendGossip(ClusterInfo clusterInfo, IPEndPoint serverEndPoint) {
				ClusterInfo = clusterInfo;
				ServerEndPoint = serverEndPoint;
			}
		}

		public class GossipUpdated : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly ClusterInfo ClusterInfo;

			public GossipUpdated(ClusterInfo clusterInfo) {
				ClusterInfo = clusterInfo;
			}
		}

		public class GossipSendFailed : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly string Reason;
			public readonly IPEndPoint Recipient;

			public GossipSendFailed(string reason, IPEndPoint recipient) {
				Reason = reason;
				Recipient = recipient;
			}

			public override string ToString() {
				return String.Format("Reason: {0}, Recipient: {1}", Reason, Recipient);
			}
		}
	}
}
