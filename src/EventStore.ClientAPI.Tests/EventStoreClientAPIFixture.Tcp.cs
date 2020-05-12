using System;
using System.Net;

namespace EventStore.ClientAPI.Tests {
	partial class EventStoreClientAPIFixture {
		private const bool UseLoggerBridge = true;

		public IEventStoreConnection CreateConnection(
			Func<ConnectionSettingsBuilder, ConnectionSettingsBuilder> configureSettings = default,
			int? port = default, bool useDnsEndPoint = false) {
			var settings = (configureSettings ?? DefaultConfigureSettings)(DefaultBuilder).Build();
			return EventStoreConnection.Create(
				settings,
				useDnsEndPoint
					? (EndPoint)new DnsEndPoint("localhost", port ?? (settings.UseSslConnection
						? ExternalSecurePort
						: ExternalPort))
					: new IPEndPoint(IPAddress.Loopback, port ?? (settings.UseSslConnection
						? ExternalSecurePort
						: ExternalPort)));
		}
	}
}
