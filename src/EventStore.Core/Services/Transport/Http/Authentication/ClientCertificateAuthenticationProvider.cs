using EventStore.Core.Services.UserManagement;
using Microsoft.AspNetCore.Http;

namespace EventStore.Core.Services.Transport.Http.Authentication {
	public class ClientCertificateAuthenticationProvider : IHttpAuthenticationProvider {
		public bool Authenticate(HttpContext context, out HttpAuthenticationRequest request) {
			request = null;
			if (context.Connection.ClientCertificate is null) return false;
			request = new HttpAuthenticationRequest(context, "system", "");
			request.Authenticated(SystemAccounts.System);
			return true;
		}
	}
}
