using System;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using EventStore.Common.Utils;
using EventStore.Core.Cluster;
using EventStore.Core.Services.Gossip;
using EventStore.Core.Services.UserManagement;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace EventStore.Core.Services.Transport.Http.Authentication {
	public class ClientCertificateAuthenticationProvider : IHttpAuthenticationProvider {
		private string _certificateReservedNodeCommonName;
		private EndPoint[] _gossipSeed;

		public ClientCertificateAuthenticationProvider(string certificateReservedNodeCommonName, EndPoint[] gossipSeed) {
			_certificateReservedNodeCommonName = certificateReservedNodeCommonName;
			_gossipSeed = gossipSeed;
		}

		public bool Authenticate(HttpContext context, out HttpAuthenticationRequest request) {
			request = null;
			var clientCertificate = context.Connection.ClientCertificate;
			if (clientCertificate is null) return false;
			
			bool hasReservedNodeCN = false;
			string clientCertificateCN;


			try {
				clientCertificateCN = clientCertificate.GetCommonName();
				if (clientCertificateCN == _certificateReservedNodeCommonName) {
					hasReservedNodeCN = true;
				} else {
					if (_gossipSeed != null) {
						foreach (var node in _gossipSeed) {
							if (node.GetHost() != clientCertificateCN) continue;
							hasReservedNodeCN = true;
							break;
						}
					}
				}
			} catch (CryptographicException) {
				return false;
			} catch (NullReferenceException) {
				return false;
			}

			if (!hasReservedNodeCN) {
				var ip = context.Connection.RemoteIpAddress?.ToString() ?? "<unknown>";
				Log.Error(
					"Connection from node: {ip} was denied because its CN: {clientCertificateCN} does not match with the reserved node CN: {reservedNodeCN}",
					ip, clientCertificateCN, _certificateReservedNodeCommonName);
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
