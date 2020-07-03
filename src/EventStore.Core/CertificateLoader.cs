using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace EventStore.Core {
	public static class CertificateLoader {
		public static X509Certificate2 FromStore(StoreLocation storeLocation, StoreName storeName,
			string certificateSubjectName, string certificateThumbprint) {
			var store = new X509Store(storeName, storeLocation);
			return GetCertificateFromStore(store, certificateSubjectName, certificateThumbprint);
		}

		public static X509Certificate2 FromStore(StoreName storeName, string certificateSubjectName,
        	string certificateThumbprint) {
        	var store = new X509Store(storeName);
        	return GetCertificateFromStore(store, certificateSubjectName, certificateThumbprint);
		}

		public static X509Certificate2 FromFile(
			string certificatePath,
			string privateKeyPath,
			string password) {

			if (string.IsNullOrEmpty(privateKeyPath)) {
				X509Certificate2 certificate;

				try {
					certificate = new X509Certificate2(certificatePath, password);
				} catch (CryptographicException exc) {
					throw new AggregateException("Error loading certificate file. Please verify that the correct password has been provided via the `CertificatePassword` option.", exc);
				}

				if (!certificate.HasPrivateKey) {
					throw new Exception("Expect certificate to contain a private key. " +
					                    "Please either provide a certificate that contains one or set the private key" +
					                    " via the `CertificatePrivateKeyFile` option.");
				}
				return certificate;
			}

			var privateKey = Convert.FromBase64String(string.Join(string.Empty, File.ReadAllLines(privateKeyPath)
				.Skip(1)
				.SkipLast(1)));

			using var rsa = RSA.Create();
			rsa.ImportRSAPrivateKey(new ReadOnlySpan<byte>(privateKey), out _);

			using var publicCertificate = new X509Certificate2(certificatePath, password);
			using var publicWithPrivate = publicCertificate.CopyWithPrivateKey(rsa);

			return new X509Certificate2(publicWithPrivate.Export(X509ContentType.Pfx));
		}

		private static X509Certificate2 GetCertificateFromStore(X509Store store, string certificateSubjectName,
			string certificateThumbprint) {
			try {
				store.Open(OpenFlags.OpenExistingOnly);
			} catch (Exception exc) {
				throw new Exception(
					string.Format("Could not open certificate store '{0}' in location {1}'.", store.Name,
						store.Location), exc);
			}

			if (!string.IsNullOrWhiteSpace(certificateThumbprint)) {
				var certificates = store.Certificates.Find(X509FindType.FindByThumbprint, certificateThumbprint, true);
				if (certificates.Count == 0)
					throw new Exception(string.Format("Could not find valid certificate with thumbprint '{0}'.",
						certificateThumbprint));

				//Can this even happen?
				if (certificates.Count > 1)
					throw new Exception(string.Format("Could not determine a unique certificate from thumbprint '{0}'.",
						certificateThumbprint));

				return certificates[0];
			}

			if (!string.IsNullOrWhiteSpace(certificateSubjectName)) {
				var certificates =
					store.Certificates.Find(X509FindType.FindBySubjectName, certificateSubjectName, true);
				if (certificates.Count == 0)
					throw new Exception(string.Format("Could not find valid certificate with subject name '{0}'.",
						certificateSubjectName));

				//Can this even happen?
				if (certificates.Count > 1)
					throw new Exception(string.Format(
						"Could not determine a unique certificate from subject name '{0}'.", certificateSubjectName));

				return certificates[0];
			}

			throw new ArgumentException(
				"No thumbprint or subject name was specified for a certificate, but a certificate store was specified.");
		}

		public static X509Certificate2Collection LoadCertificateCollection(string path) {
			var certCollection = new X509Certificate2Collection();
			var files = Directory.GetFiles(path);
			var acceptedExtensions = new[] {".crt", ".cert", ".cer", ".pem", ".der"};
			foreach (var file in files) {
				var fileInfo = new FileInfo(file);
				var extension = fileInfo.Extension;
				if (acceptedExtensions.Contains(extension)) {
					try {
						var cert = new X509Certificate2(File.ReadAllBytes(file));
						certCollection.Add(cert);
						//_log.Information("Trusted root certificate file loaded: {file}", fileInfo.Name);
					} catch (Exception exc) {
						throw new AggregateException($"Error loading trusted root certificate file: {file}", exc);
					}
				}
			}

			if (certCollection.Count == 0)
				throw new Exception($"No trusted root certificates were loaded from: {path}");

			return certCollection;
		}
	}
}
