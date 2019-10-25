using System;
using System.Net;
using EventStore.ClientAPI;

namespace EventStore.ClientAPI.Tests {
	partial class EventStoreClientAPIFixture {
		private const bool UseLoggerBridge = true;

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
