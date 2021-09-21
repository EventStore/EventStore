using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace EventStore.Core.Tests.Certificates {
	public class with_certificates : SpecificationWithDirectoryPerTestFixture {
		private static readonly Random Random = new();
		protected static X509Certificate2 CreateCertificate(bool issuer, X509Certificate2 parent = null, bool expired = false) {
			using var rsa = RSA.Create();
			var certReq = new CertificateRequest(GenerateSubject(), rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
			var now = DateTimeOffset.UtcNow;
			now = new DateTime(now.Year, now.Month, now.Day); // round to nearest day
			var startDate = now.AddMonths(-1);
			var endDate = !expired ? now.AddMonths(1) : now.AddDays(-1);

			if (issuer) {
				certReq.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
			}
			if (parent == null) {
				return certReq.CreateSelfSigned(startDate, endDate);
			}

			var parentKey = (RSA) parent!.PrivateKey!;
			var signatureGenerator = X509SignatureGenerator.CreateForRSA(parentKey!, RSASignaturePadding.Pkcs1);
			return certReq.Create(parent.SubjectName, signatureGenerator, startDate, endDate, GenerateSerialNumber()).CopyWithPrivateKey(rsa);
		}

		private static string GetCharset() {
			var charset = "";
			for (var c = 'a'; c <= 'z'; c++) {
				charset += c;
			}
			for (var c = 'A'; c <= 'Z'; c++) {
				charset += c;
			}
			for (var c = '0'; c <= '9'; c++) {
				charset += c;
			}
			return charset;
		}

		private static byte[] GenerateSerialNumber() {
			var charset = GetCharset();
			string s = "";
			for (var j = 0; j < 10; j++) {
				s += charset[Random.Next() % charset.Length];
			}

			var utf8Encoding = new UTF8Encoding(false);
			return utf8Encoding.GetBytes(s);
		}

		private static string GenerateSubject() {
			var charset = GetCharset();
			string s = "";
			for (var j = 0; j < 10; j++) {
				s += charset[Random.Next() % charset.Length];
			}

			return $"CN={s}";
		}
	}
}
