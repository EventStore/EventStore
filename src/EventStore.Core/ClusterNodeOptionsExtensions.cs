using System.Security.Cryptography.X509Certificates;
using EventStore.Common.Exceptions;
using EventStore.Common.Utils;
using Serilog;

namespace EventStore.Core {
	public static class ClusterNodeOptionsExtensions {
		/// <summary>
		/// </summary>
		/// <param name="options"></param>
		/// <returns></returns>
		/// <exception cref="InvalidConfigurationException"></exception>
		public static (X509Certificate2 certificate,X509Certificate2Collection intermediates) LoadNodeCertificate(this ClusterNodeOptions options) {
			if (options.ServerCertificate != null) { //used by test code paths only
				return (options.ServerCertificate!, null);
			}
			if (!string.IsNullOrWhiteSpace(options.CertificateStoreLocation)) {
				var location =
					CertificateUtils.GetCertificateStoreLocation(options.CertificateStoreLocation);
				var name = CertificateUtils.GetCertificateStoreName(options.CertificateStoreName);
				return (CertificateUtils.LoadFromStore(location, name, options.CertificateSubjectName, options.CertificateThumbprint), null);
			}

			if (!string.IsNullOrWhiteSpace(options.CertificateStoreName)) {
				var name = CertificateUtils.GetCertificateStoreName(options.CertificateStoreName);
				return (CertificateUtils.LoadFromStore(name, options.CertificateSubjectName, options.CertificateThumbprint), null);
			}

			if (options.CertificateFile.IsNotEmptyString()) {
				Log.Information("Loading the node's certificate(s) from file: {path}", options.CertificateFile);
				return CertificateUtils.LoadFromFile(options.CertificateFile, options.CertificatePrivateKeyFile, options.CertificatePassword);
			}

			throw new InvalidConfigurationException(
				"A certificate is required unless insecure mode (--insecure) is set.");
		}

		/// <summary>
		/// Loads an <see cref="X509Certificate2Collection"/> from the options set
		/// </summary>
		/// <param name="options"></param>
		/// <returns></returns>
		/// <exception cref="InvalidConfigurationException"></exception>
		public static X509Certificate2Collection LoadTrustedRootCertificates(this ClusterNodeOptions options) {
			if (options.TrustedRootCertificates != null) { //used by test code paths only
				return options.TrustedRootCertificates;
			}
			var trustedRootCerts = new X509Certificate2Collection();
			if (string.IsNullOrEmpty(options.TrustedRootCertificatesPath)) {
				throw new InvalidConfigurationException(
					$"{nameof(options.TrustedRootCertificatesPath)} must be specified unless insecure mode (--insecure) is set.");
			}
			Log.Information("Loading trusted root certificates.");
			foreach (var (fileName, cert) in CertificateUtils.LoadAllCertificates(options.TrustedRootCertificatesPath)) {
				trustedRootCerts.Add(cert);
				Log.Information("Loading trusted root certificate file: {file}", fileName);
			}

			if (trustedRootCerts.Count == 0)
				throw new InvalidConfigurationException(
					$"No trusted root certificate files were loaded from the specified path: {options.TrustedRootCertificatesPath}");
			return trustedRootCerts;
		}
	}
}
