using System;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using EventStore.Core.Services.UserManagement;
using Microsoft.AspNetCore.Http;

namespace EventStore.Core.Services.Transport.Http.Authentication {
	public class ClientCertificateAuthenticationProvider : IHttpAuthenticationProvider {
		private readonly string _certificateReservedNodeCommonName;

		public ClientCertificateAuthenticationProvider(string certificateReservedNodeCommonName) {
			_certificateReservedNodeCommonName = certificateReservedNodeCommonName;
		}

		public bool Authenticate(HttpContext context, out HttpAuthenticationRequest request) {
			request = null;
			var clientCertificate = context.Connection.ClientCertificate;
			if (clientCertificate is null)
				return false;

			bool hasReservedNodeCN;
			try {
				hasReservedNodeCN = clientCertificate.GetNameInfo(X509NameType.SimpleName, false) == _certificateReservedNodeCommonName;
			} catch (CryptographicException) {
				return false;
			} catch (NullReferenceException) {
				return false;
			}

			bool hasIpOrDnsSan = false;
			X509ExtensionCollection extensions;
			try {
				extensions = clientCertificate.Extensions;
			} catch (CryptographicException) {
				return false;
			}
			foreach (var extension in extensions) {
				AsnEncodedData asnData = new AsnEncodedData(extension.Oid, extension.RawData);
				if (extension.Oid.Value == "2.5.29.17") { //Oid for Subject Alternative Names extension
					var data = asnData.Format(false);
					string[] parts = data.Split(new[] { ':', '=', ',' }, StringSplitOptions.RemoveEmptyEntries);
					var acceptedHeaders = new[] { "DNS", "DNS Name", "IP", "IP Address" };
					for (int i = 0; i < parts.Length; i += 2) {
						var header = parts[i].Trim();
						if (acceptedHeaders.Any(x => x == header)) {
							hasIpOrDnsSan = true;
							break;
						}
					}
				}
			}

			if (hasReservedNodeCN && hasIpOrDnsSan) {
				request = new HttpAuthenticationRequest(context, "system", "");
				request.Authenticated(SystemAccounts.System);
				return true;
			}

			return false;
		}
	}
}
