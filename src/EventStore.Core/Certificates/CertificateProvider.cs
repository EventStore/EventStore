using System.Security.Cryptography.X509Certificates;

namespace EventStore.Core.Certificates {
	public abstract class CertificateProvider {
		public X509Certificate2 Certificate;
		public X509Certificate2Collection IntermediateCerts;
		public X509Certificate2Collection TrustedRootCerts;

		public abstract LoadCertificateResult LoadCertificates(ClusterVNodeOptions options);
	}

	public enum LoadCertificateResult {
		Success = 1,
		VerificationFailed,
		Skipped
	}
}
