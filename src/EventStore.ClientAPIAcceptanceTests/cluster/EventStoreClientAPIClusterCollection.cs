using Xunit;

namespace EventStore.ClientAPI.Tests {

	[CollectionDefinition(nameof(EventStoreClientAPIClusterCollection))]
	public class EventStoreClientAPIClusterCollection : ICollectionFixture<EventStoreClientAPIClusterFixture> {}
}
