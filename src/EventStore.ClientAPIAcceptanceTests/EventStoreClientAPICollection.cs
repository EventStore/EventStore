using Xunit;

namespace EventStore.ClientAPI.Tests {

	[CollectionDefinition(nameof(EventStoreClientAPICollection))]
	public class EventStoreClientAPICollection : ICollectionFixture<EventStoreClientAPIFixture> {}
}
