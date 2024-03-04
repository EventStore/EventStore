using EventStore.Core.Services.UserManagement;
using EventStore.Plugins.Authentication;
using Microsoft.AspNetCore.Http;

namespace EventStore.Core.Services.Transport.Http.Authentication {
	public class AnonymousHttpAuthenticationProvider : IHttpAuthenticationProvider {
		public string Name => "anonymous";

		public bool Authenticate(HttpContext context, out HttpAuthenticationRequest request) {
			request = new HttpAuthenticationRequest(context, null, null);
			request.Authenticated(SystemAccounts.Anonymous);
			return true;
		}
	}
}
