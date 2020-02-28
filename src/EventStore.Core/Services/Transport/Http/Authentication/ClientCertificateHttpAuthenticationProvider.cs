using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using EventStore.Core.Authentication;
using Microsoft.AspNetCore.Http;

namespace EventStore.Core.Services.Transport.Http.Authentication {
	public class ClientCertificateHttpAuthenticationProvider : IHttpAuthenticationProvider {
		private readonly IAuthenticationProvider _internalAuthenticationProvider;

		public ClientCertificateHttpAuthenticationProvider(IAuthenticationProvider internalAuthenticationProvider) {
			_internalAuthenticationProvider = internalAuthenticationProvider;
		}

		public bool Authenticate(HttpContext context, out HttpAuthenticationRequest request) {
			if (context.Connection.ClientCertificate != null) {
				var certificate = context.Connection.ClientCertificate;
				var commonName = certificate.GetNameInfo(X509NameType.SimpleName, false);
				if (commonName != null) {
					request = new HttpAuthenticationRequest(context, commonName, null, certificate);
					_internalAuthenticationProvider.Authenticate(request);
					return true;
				}
			}

			request = null;
			return false;
		}
	}
}
