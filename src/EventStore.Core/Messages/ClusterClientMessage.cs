using EventStore.Core.Messaging;

namespace EventStore.Core.Messages {
	public static partial class ClusterClientMessage {
		[DerivedMessage]
		public partial class CleanCache : Message {
			public CleanCache() { }
		}
	}
}
