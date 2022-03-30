using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace EventStore.Common.Utils {
	public static class CertificateDelegates {
		public delegate (bool, string) ServerCertificateValidator(X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors, string[] otherNames);
		public delegate (bool, string) ClientCertificateValidator(X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors);
	}
}
