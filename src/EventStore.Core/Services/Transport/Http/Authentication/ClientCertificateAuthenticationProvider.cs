using System;
using System.Security.Cryptography.X509Certificates;
using EventStore.Core.Services.UserManagement;
using Microsoft.AspNetCore.Http;

namespace EventStore.Core.Services.Transport.Http.Authentication {
	public class ClientCertificateAuthenticationProvider : IHttpAuthenticationProvider {
		private readonly Func<X509Chain, (bool, string)> _validateNodeClientCertificate;
		private readonly X509Certificate2Collection _trustedRootCerts;

		public ClientCertificateAuthenticationProvider(
			Func<X509Chain, ValueTuple<bool, string>> validateNodeClientCertificate,
			X509Certificate2Collection trustedRootCerts) {
			_validateNodeClientCertificate = validateNodeClientCertificate;
			_trustedRootCerts = trustedRootCerts;
		}

		public bool Authenticate(HttpContext context, out HttpAuthenticationRequest request) {
			request = null;
			if (context.Connection.ClientCertificate is null) return false;

			var newChain = new X509Chain {
				ChainPolicy = {
					RevocationMode = X509RevocationMode.NoCheck
				}
			};

			if (_trustedRootCerts != null) {
				foreach (var cert in _trustedRootCerts)
					newChain.ChainPolicy.ExtraStore.Add(cert);
			}

			newChain.Build(new X509Certificate2(context.Connection.ClientCertificate));

			var (isValid, msg) = _validateNodeClientCertificate(newChain);
			if (!isValid) return false;

			request = new HttpAuthenticationRequest(context, "system", "");
			request.Authenticated(SystemAccounts.System);
			return true;
		}
	}
}
