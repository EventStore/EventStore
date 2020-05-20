using System;

namespace EventStore.ClientAPI.Tests {
	partial class EventStoreClientAPIClusterFixture {
		private ConnectionSettingsBuilder DefaultBuilder {
			get {
				var builder = ConnectionSettings.Create()
					.EnableVerboseLogging()
					.LimitReconnectionsTo(10)
					.LimitAttemptsForOperationTo(1)
					.SetTimeoutCheckPeriodTo(TimeSpan.FromMilliseconds(100))
					.SetReconnectionDelayTo(TimeSpan.Zero)
					.FailOnNoServerResponse();
#pragma warning disable 0162
#if DEBUG
				if (UseLoggerBridge) {
					builder = builder.UseCustomLogger(ConsoleLoggerBridge.Default);
				}
#endif
#pragma warning restore 0162
				return builder;
			}
		}

		private static ConnectionSettingsBuilder DefaultConfigureSettings(
			ConnectionSettingsBuilder builder)
			=> builder;
		
		private static string DefaultConfigureSettingsForConnectionString =>
			"VerboseLogging=true; MaxReconnections=10;MaxRetries=1;OperationTimeoutCheckPeriod=100;ReconnectionDelay=0;FailOnNoServerResponse=true;";

	}
}
