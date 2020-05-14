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
				new IPEndPoint(IPAddress.Loopback, port ?? (settings.UseSslConnection
					                                   ? ExternalSecurePort
					                                   : ExternalPort)));
		}

		public IEventStoreConnection CreateConnectionWithConnectionString(
			bool useSsl,
			string configureSettings = default,
			int? port = default, bool useDnsEndPoint = false) {
			var settings = configureSettings ?? DefaultConfigureSettingsForConnectionString;
			var host = useDnsEndPoint ? "localhost" : IPAddress.Loopback.ToString();
			port ??= (useSsl ? ExternalSecurePort : ExternalPort);

			if (useSsl) settings += $"UseSslConnection=true;ValidateServer=false;TargetHost={host};";

			var connectionString = $"ConnectTo=tcp://{host}:{port};{settings}";
			return EventStoreConnection.Create(connectionString);
		}
	}
}
