using System;
using System.Net;
using EventStore.ClientAPI;

namespace EventStore.ClientAPIAcceptanceTests {
	partial class EventStoreClientAPIFixture {
		public IEventStoreConnection CreateConnection(
			Func<ConnectionSettingsBuilder, ConnectionSettingsBuilder> configureSettings = default,
			int? port = default) {
			var settings = (configureSettings ?? DefaultConfigureSettings)(DefaultBuilder).Build();
			return EventStoreConnection.Create(
				settings,
				new IPEndPoint(IPAddress.Loopback, port ?? (settings.UseSslConnection
					                                   ? ExternalSecurePort
					                                   : ExternalPort)));
		}
	}
}
