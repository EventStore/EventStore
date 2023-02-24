using EventStore.Core.Messaging;

namespace EventStore.Core.Messages {
	public static partial class ClusterClientMessage {
		[DerivedMessage(CoreMessage.ClusterClient)]
		public partial class CleanCache : Message {
			public CleanCache() { }
		}
	}
}
