using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace EventStore.ClientAPI.Tests {
	[Collection(nameof(EventStoreClientAPICollection))]
	public abstract class EventStoreClientAPITest {
		public string GetStreamName([CallerMemberName] string testMethod = default)
			=> $"{GetType().Name}_{testMethod ?? "unknown"}";

		#if CLIENT_API_V5
		public static IEnumerable<SslType> SslTypes => new[] {SslType.None, SslType.NoClientCertificate};
		#endif

		#if CLIENT_API_EMBEDDED
		public static IEnumerable<SslType> SslTypes => new[] {SslType.None};
		#endif

		#if CLIENT_API
		public static IEnumerable<SslType> SslTypes => new[] {SslType.None, SslType.NoClientCertificate, SslType.WithAdminClientCertificate};
		#endif

		protected static IEnumerable<(long expectedVersion, string displayName)> ExpectedVersions
			=> new[] {
				((long)ExpectedVersion.Any, nameof(ExpectedVersion.Any)),
				(ExpectedVersion.NoStream, nameof(ExpectedVersion.NoStream))
			};

		public static IEnumerable<object[]> UseSslTestCases() {
			var testCases = SslTypes.Select(sslType => new object[] {sslType});
			if (!IsCACertificateInstalled()) {
				return testCases.Where(x => (SslType)x[0] != SslType.WithAdminClientCertificate);
			}

			return testCases;
		}

		private static bool IsCACertificateInstalled() {
			var clientCertificate = GetClientCertificate();
			using (X509Chain chain = new X509Chain())
			{
				chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
				return chain.Build(clientCertificate);
			}
		}

		private static X509Certificate2 GetClientCertificate() {
			using var stream =
				typeof(EventStoreClientAPIFixture).Assembly.GetManifestResourceStream(
					typeof(EventStoreClientAPIFixture), "test_certificates.client_admin.client_admin.p12");
			using var mem = new MemoryStream();
			stream.CopyTo(mem);
			return new X509Certificate2(mem.ToArray(), "password");
		}

		public static IEnumerable<object[]> ExpectedVersionTestCases() {
			foreach (var (expectedVersion, displayName) in ExpectedVersions)
			foreach (var sslType in SslTypes) {
				yield return new object[] {expectedVersion, displayName, sslType};
			}
		}
	}
}
