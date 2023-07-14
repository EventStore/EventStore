using System.Security.Cryptography.X509Certificates;

namespace EventStore.Core.Certificates {
	public class DevCertificateProvider : CertificateProvider {
		public DevCertificateProvider(X509Certificate2 certificate) {
			Certificate = certificate;
			TrustedRootCerts = new X509Certificate2Collection(certificate);
		}

		public override LoadCertificateResult LoadCertificates(ClusterVNodeOptions options) {
			return LoadCertificateResult.Skipped;
		}
	}
}
