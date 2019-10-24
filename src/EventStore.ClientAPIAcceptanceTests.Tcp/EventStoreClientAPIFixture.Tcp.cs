using System.Net;
using EventStore.ClientAPI;

namespace EventStore.ClientAPIAcceptanceTests {
	partial class EventStoreClientAPIFixture {
		public IEventStoreConnection CreateConnection(ConnectionSettings settings = default, int? port = default)
			=> EventStoreConnection.Create(settings ?? Settings(), new IPEndPoint(IPAddress.Loopback, port ?? ExternalPort));
	}
}
