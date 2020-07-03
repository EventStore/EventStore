using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using EventStore.Core.Exceptions;

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
				var certificate = new X509Certificate2(certificatePath, password);

				if (!certificate.HasPrivateKey)
					throw new NoCertificatePrivateKeyException();

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

		public static IEnumerable<(string fileName,X509Certificate2 certificate)> LoadAllCertificates(string path) {
			var files = Directory.GetFiles(path);
			var acceptedExtensions = new[] {".crt", ".cert", ".cer", ".pem", ".der"};
			foreach (var file in files) {
				var fileInfo = new FileInfo(file);
				var extension = fileInfo.Extension;
				if (acceptedExtensions.Contains(extension)) {
					X509Certificate2 cert;
					try {
						cert = new X509Certificate2(File.ReadAllBytes(file));
					} catch (Exception exc) {
						throw new AggregateException($"Error loading certificate file: {file}", exc);
					}
					yield return (file, cert);
				}
			}
		}
	}
}
