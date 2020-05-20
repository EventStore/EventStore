using System;

namespace EventStore.ClientAPI.Tests {
	internal static class ConnectionSettingsBuilderExtensions {
		#if CLIENT_API_V5
		public static ConnectionSettingsBuilder UseSsl(this ConnectionSettingsBuilder builder, bool useSsl)
			=> useSsl ? builder.UseSslConnection(Guid.NewGuid().ToString("n"), false) : builder;
		#elif CLIENT_API || CLIENT_API_EMBEDDED
		public static ConnectionSettingsBuilder UseSsl(this ConnectionSettingsBuilder builder, bool useSsl)
			=> useSsl ? builder.DisableServerCertificateValidation() : builder.DisableTls();
		#endif
	}
}
