using System.Security.Claims;
using EventStore.Core.Services.Transport.Http.Messages;

namespace EventStore.Core.Services.Transport.Http.Authentication {
	public class AnonymousHttpAuthenticationProvider : HttpAuthenticationProvider {
		public override bool Authenticate(IncomingHttpRequestMessage message) {
			switch (message.Entity.User) {
				case ClaimsPrincipal claimsPrincipal when !claimsPrincipal.Identity.IsAuthenticated:
				case null:
					Authenticated(message, user: null);
					return true;
				default:
					return false;
			}
		}
	}
}
