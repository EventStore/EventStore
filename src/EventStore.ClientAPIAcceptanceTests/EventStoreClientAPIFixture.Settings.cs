using System;

namespace EventStore.ClientAPI.Tests {
	partial class EventStoreClientAPIFixture {
		private ConnectionSettingsBuilder DefaultBuilder {
			get {
				var builder = ConnectionSettings.Create()
					.EnableVerboseLogging()
					.LimitReconnectionsTo(10)
					.LimitRetriesForOperationTo(100)
					.SetTimeoutCheckPeriodTo(TimeSpan.FromMilliseconds(100))
					.SetReconnectionDelayTo(TimeSpan.Zero)
					.FailOnNoServerResponse();

				// ReSharper disable ConditionIsAlwaysTrueOrFalse
				// ReSharper disable HeuristicUnreachableCode
#if DEBUG
				if (UseLoggerBridge) {
					builder = builder.UseCustomLogger(ConsoleLoggerBridge.Default);
				}
				// ReSharper restore HeuristicUnreachableCode
				// ReSharper restore ConditionIsAlwaysTrueOrFalse
#endif
				return builder;
			}
		}

		private static ConnectionSettingsBuilder DefaultConfigureSettings(
			ConnectionSettingsBuilder builder)
			=> builder;

	}
}
