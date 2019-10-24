using EventStore.ClientAPI;
using EventStore.ClientAPI.Embedded;

namespace EventStore.ClientAPIAcceptanceTests {
	partial class EventStoreClientAPIFixture {
		public IEventStoreConnection CreateConnection(ConnectionSettings settings = default, int? port = default)
			=> EmbeddedEventStoreConnection.Create(_node, settings ?? Settings());
	}
}
