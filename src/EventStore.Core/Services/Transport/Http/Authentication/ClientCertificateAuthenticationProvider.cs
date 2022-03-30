using System;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using EventStore.Common.Utils;
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
			if (clientCertificate is null) return false;

			bool hasReservedNodeCN;
			try {
				hasReservedNodeCN = clientCertificate.GetNameInfo(X509NameType.SimpleName, false) == _certificateReservedNodeCommonName;
			} catch (CryptographicException) {
				return false;
			} catch (NullReferenceException) {
				return false;
			}

			bool hasIpOrDnsSan = clientCertificate.GetSubjectAlternativeNames()
				.Where(x => x.type is CertificateNameType.DnsName or CertificateNameType.IpAddress)
				.IsNotEmpty();

			if (hasReservedNodeCN && hasIpOrDnsSan) {
				request = new HttpAuthenticationRequest(context, "system", "");
				request.Authenticated(SystemAccounts.System);
				return true;
			}

			return false;
		}
	}
}
