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

			public readonly EndPoint[] GossipSeeds;

			public GotGossipSeedSources(EndPoint[] gossipSeeds) {
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
			public readonly EndPoint Server;

			public GossipReceived(IEnvelope envelope, ClusterInfo clusterInfo, EndPoint server) {
				Envelope = envelope;
				ClusterInfo = clusterInfo;
				Server = server;
			}
		}

		public class ReadGossip : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly IEnvelope Envelope;

			public ReadGossip(IEnvelope envelope) {
				Envelope = envelope;
			}
		}

		public class SendGossip : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly ClusterInfo ClusterInfo;
			public readonly EndPoint ServerEndPoint;

			public SendGossip(ClusterInfo clusterInfo, EndPoint serverEndPoint) {
				ClusterInfo = clusterInfo;
				ServerEndPoint = serverEndPoint;
			}
		}

		public class ClientGossip : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly IEnvelope Envelope;

			public ClientGossip(IEnvelope envelope) {
				Envelope = envelope;
			}
		}

		public class SendClientGossip : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly ClientClusterInfo ClusterInfo;

			public SendClientGossip(ClientClusterInfo clusterInfo) {
				ClusterInfo = clusterInfo;
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
			public readonly EndPoint Recipient;

			public GossipSendFailed(string reason, EndPoint recipient) {
				Reason = reason;
				Recipient = recipient;
			}

			public override string ToString() {
				return String.Format("Reason: {0}, Recipient: {1}", Reason, Recipient);
			}
		}

		public class GetGossip : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public GetGossip() { }
		}

		public class GetGossipFailed : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

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

		public class GetGossipReceived : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly ClusterInfo ClusterInfo;
			public readonly EndPoint Server;

			public GetGossipReceived(ClusterInfo clusterInfo, EndPoint server) {
				ClusterInfo = clusterInfo;
				Server = server;
			}
		}

		public class UpdateNodePriority : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly int NodePriority;

			public UpdateNodePriority(int priority) {
				NodePriority = priority;
			}
		}
	}
}
