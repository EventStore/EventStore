using System;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Embedded;

namespace EventStore.ClientAPI.Tests {
	partial class EventStoreClientAPIFixture {
		private const bool UseLoggerBridge = false;

		public IEventStoreConnection CreateConnection(
			Func<ConnectionSettingsBuilder, ConnectionSettingsBuilder> configureSettings = default, int? port = default)
			=> EmbeddedEventStoreConnection.Create(_node,
				(configureSettings ?? DefaultConfigureSettings)(DefaultBuilder));
	}
}
