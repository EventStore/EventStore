using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace EventStore.ClientAPI.Tests {
	public enum SslType {
		None,
		NoClientCertificate,
		WithAdminClientCertificate
	}
	internal static class ConnectionSettingsBuilderExtensions {

		public static ConnectionSettingsBuilder UseSsl(this ConnectionSettingsBuilder builder, SslType connType) {
			switch (connType) {
				case SslType.None:
					return builder;
				case SslType.NoClientCertificate:
					#if CLIENT_API_EMBEDDED
					throw new NotImplementedException();
					#else
					return builder.UseSslConnection(Guid.NewGuid().ToString("n"), false);
					#endif
				case SslType.WithAdminClientCertificate:
					#if CLIENT_API_V5 || CLIENT_API_EMBEDDED
					throw new NotImplementedException();
					#else
					using (var stream = typeof(EventStoreClientAPIFixture).Assembly.GetManifestResourceStream(typeof(EventStoreClientAPIFixture), "test_certificates.client_admin.client_admin.p12")) {
						using (var mem = new MemoryStream()) {
							stream.CopyTo(mem);
							return builder.UseSslConnection(
								Guid.NewGuid().ToString("n"),
								false,
								new X509Certificate2(mem.ToArray(), "password")
							);
						}
					}
					#endif
			}
			throw new InvalidOperationException();
		}
	}
}
