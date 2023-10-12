using System;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using EventStore.Common.Utils;
using EventStore.Core.Services.UserManagement;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace EventStore.Core.Services.Transport.Http.Authentication {
	public class ClientCertificateAuthenticationProvider : IHttpAuthenticationProvider {
		private readonly Func<string> _getCertificateReservedNodeCommonName;

		public ClientCertificateAuthenticationProvider(Func<string> getCertificateReservedNodeCommonName) {
			_getCertificateReservedNodeCommonName = getCertificateReservedNodeCommonName;
		}

		public bool Authenticate(HttpContext context, out HttpAuthenticationRequest request) {
			request = null;
			var clientCertificate = context.Connection.ClientCertificate;
			if (clientCertificate is null) return false;

			var reservedNodeCN = _getCertificateReservedNodeCommonName();
			bool hasReservedNodeCN;
			try {
				hasReservedNodeCN = clientCertificate.ClientCertificateMatchesName(reservedNodeCN);
			} catch (CryptographicException) {
				return false;
			} catch (NullReferenceException) {
				return false;
			}

			if (!hasReservedNodeCN) {
				var clientCertificateCN = clientCertificate.GetCommonName();
				var ip = context.Connection.RemoteIpAddress?.ToString() ?? "<unknown>";
				Log.Error(
					"Connection from node: {ip} was denied because its CN: {clientCertificateCN} does not match with the reserved node CN: {reservedNodeCN}",
					ip, clientCertificateCN, reservedNodeCN);
				return false;
			}

			bool hasIpOrDnsSan = clientCertificate.GetSubjectAlternativeNames()
				.Where(x => x.type is CertificateNameType.DnsName or CertificateNameType.IpAddress)
				.IsNotEmpty();

			if (!hasIpOrDnsSan) {
				var ip = context.Connection.RemoteIpAddress?.ToString() ?? "<unknown>";
				Log.Error("Connection from node: {ip} was denied because its certificate does not have any IP or DNS Subject Alternative Names (SAN).", ip);
				return false;
			}
			
			request = new HttpAuthenticationRequest(context, "system", "");
			request.Authenticated(SystemAccounts.System);
			return true;
		}
	}
}
