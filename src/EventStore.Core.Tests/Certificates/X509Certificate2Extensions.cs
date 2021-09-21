using System.Security.Cryptography.X509Certificates;

namespace EventStore.Core.Tests.Certificates {
	public static class X509Certificate2Extensions {
		public static string PemPrivateKey(this X509Certificate2 certificate) {
			return certificate.GetRSAPrivateKey()!.ExportRSAPrivateKey().PEM("PRIVATE KEY");
		}
	}
}
