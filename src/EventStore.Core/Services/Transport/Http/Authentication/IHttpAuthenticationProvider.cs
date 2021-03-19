using Microsoft.AspNetCore.Http;

namespace EventStore.Core.Services.Transport.Http.Authentication {
	public interface IHttpAuthenticationProvider {
		bool Authenticate(HttpContext context, out HttpAuthenticationRequest request);
	}
}
