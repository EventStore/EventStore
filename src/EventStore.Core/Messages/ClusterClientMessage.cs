using EventStore.Core.Messaging;

namespace EventStore.Core.Messages {
	public static class ClusterClientMessage {
		public class CleanCache : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public CleanCache() { }
		}
	}
}
