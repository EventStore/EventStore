using System.Security.Cryptography.X509Certificates;
using EventStore.Common.Utils;
using Serilog;

namespace EventStore.Core.Certificates {
	public class OptionsCertificateProvider: CertificateProvider {
		private static readonly ILogger Log = Serilog.Log.ForContext<ClusterVNode>();
		public override LoadCertificateResult LoadCertificates(ClusterVNodeOptions options) {
			if (options.Application.Insecure) {
				Log.Information("Skipping reload of certificates since TLS is disabled.");
				return LoadCertificateResult.Skipped;
			}

			var (certificate, intermediates) = options.LoadNodeCertificate();

			var certificateCN = certificate.GetCommonName();
			var optionsCertificateCN = options.Certificate.CertificateReservedNodeCommonName;
			
			if (optionsCertificateCN is not null && certificateCN != optionsCertificateCN) {
				Log.Error(
					"Certificate CN: {certificateCN} does not match with the CertificateReservedNodeCommonName configuration setting: {reservedNodeCN}",
					certificateCN, optionsCertificateCN);
				return LoadCertificateResult.VerificationFailed;
			}

			CertificateCN = certificateCN;
			
			var previousThumbprint = Certificate?.Thumbprint;
			var newThumbprint = certificate.Thumbprint;
			Log.Information("Loading the node's certificate. Subject: {subject}, Previous thumbprint: {previousThumbprint}, New thumbprint: {newThumbprint}",
				certificate.SubjectName.Name, previousThumbprint, newThumbprint);
			
			if (intermediates != null) {
				foreach (var intermediateCert in intermediates) {
					Log.Information("Loading intermediate certificate. Subject: {subject}, Thumbprint: {thumbprint}", intermediateCert.SubjectName.Name, intermediateCert.Thumbprint);
				}
			}

			var trustedRootCerts = options.LoadTrustedRootCertificates();

			foreach (var trustedRootCert in trustedRootCerts) {
				Log.Information("Loading trusted root certificate. Subject: {subject}, Thumbprint: {thumbprint}", trustedRootCert.SubjectName.Name, trustedRootCert.Thumbprint);
			}

			if (!VerifyCertificates(certificate, intermediates, trustedRootCerts)) {
				return LoadCertificateResult.VerificationFailed;
			}

			//no need for a lock here since reference assignment is atomic
			Certificate = certificate;
			IntermediateCerts = intermediates;
			TrustedRootCerts = trustedRootCerts;

			Log.Information("All certificates successfully loaded.");
			return LoadCertificateResult.Success;
		}

		private static bool VerifyCertificates(X509Certificate2 nodeCertificate, X509Certificate2Collection intermediates, X509Certificate2Collection trustedRoots) {
			bool error = false;

			if (!CertificateUtils.IsValidNodeCertificate(nodeCertificate, out var errorMsg)) {
				Log.Error(errorMsg);
				error = true;
			}

			if (intermediates != null) {
				foreach (var cert in intermediates) {
					if (!CertificateUtils.IsValidIntermediateCertificate(cert, out errorMsg)) {
						Log.Error($"{errorMsg} Please bundle only intermediate certificates (if any) and not root certificates with the node's certificate.");
						error = true;
					}
				}
			}

			if (trustedRoots != null && trustedRoots.Count > 0) {
				foreach (var cert in trustedRoots) {
					if (!CertificateUtils.IsValidRootCertificate(cert, out errorMsg)) {
						Log.Error($"{errorMsg} If you have intermediate certificates, please bundle them with the node's certificate (in PEM or PKCS #12 format).");
						error = true;
					}
				}
			} else {
				Log.Error("No trusted root certificates loaded");
				error = true;
			}

			if (error) return false;

			var chainStatus = CertificateUtils.BuildChain(nodeCertificate, intermediates, trustedRoots, out var chainStatusInformation );

			if (chainStatus != X509ChainStatusFlags.NoError) {
				Log.Error(
					"Failed to build the certificate chain with the node's own certificate up to the root. " +
					"If you have intermediate certificates, please bundle them with the node's certificate (in PEM or PKCS #12 format). Errors:-");
				foreach (var status in chainStatusInformation) {
					Log.Error(status);
				}

				error = true;
			}

			if (!error && intermediates != null) {
				chainStatus = CertificateUtils.BuildChain(nodeCertificate, null, trustedRoots, out chainStatusInformation);

				// Adding the intermediate certificates to the store is required so that
				// i)  the full certificate chain (excluding the root) is sent from client to server (on both Windows/Linux)
				//     and from server to client (on Windows only) during the TLS connection establishment
				// ii) to prevent AIA certificate downloads
				//
				// see: https://github.com/dotnet/runtime/issues/47680#issuecomment-771093045
				// and https://github.com/dotnet/runtime/issues/59979

				if (chainStatus != X509ChainStatusFlags.NoError) {
					Log.Warning(
						"For correct functioning and optimal performance, please add your intermediate certificates to the current user's " +
							(Runtime.IsWindows ?
							"'Intermediate Certification Authorities' certificate store." :
							"'CertificateAuthority' certificate store using the dotnet-certificate-tool.")
					);
				}
			}

			if (!error) {
				Log.Information("Certificate chain verification successful.");
			}

			return !error;
		}
	}
}
