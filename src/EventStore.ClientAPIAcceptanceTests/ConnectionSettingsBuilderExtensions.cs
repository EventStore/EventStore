using System;

namespace EventStore.ClientAPI.Tests {
	internal static class ConnectionSettingsBuilderExtensions {
		public static ConnectionSettingsBuilder UseSsl(this ConnectionSettingsBuilder builder, bool useSsl)
			=> useSsl ? builder.UseSslConnection(Guid.NewGuid().ToString("n"), false) : builder;
	}
}
