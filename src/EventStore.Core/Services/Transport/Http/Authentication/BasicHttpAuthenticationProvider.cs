using System.Net;
using System.Security.Principal;
using EventStore.Core.Authentication;
using EventStore.Core.Services.Transport.Http.Messages;

namespace EventStore.Core.Services.Transport.Http.Authentication {
	public class BasicHttpAuthenticationProvider : HttpAuthenticationProvider {
		private readonly IAuthenticationProvider _internalAuthenticationProvider;

		public BasicHttpAuthenticationProvider(IAuthenticationProvider internalAuthenticationProvider) {
			_internalAuthenticationProvider = internalAuthenticationProvider;
		}

		public override bool Authenticate(IncomingHttpRequestMessage message) {
			//NOTE: this method can be invoked on multiple threads - needs to be thread safe
			var entity = message.Entity;
			var basicIdentity = entity.User != null ? entity.User.Identity as HttpListenerBasicIdentity : null;
			if (basicIdentity != null) {
				string name = basicIdentity.Name;
				string suppliedPassword = basicIdentity.Password;

				var authenticationRequest = new HttpBasicAuthenticationRequest(this, message, name, suppliedPassword);
				_internalAuthenticationProvider.Authenticate(authenticationRequest);
				return true;
			}

			return false;
		}

		private class HttpBasicAuthenticationRequest : AuthenticationRequest {
			private readonly BasicHttpAuthenticationProvider _basicHttpAuthenticationProvider;
			private readonly IncomingHttpRequestMessage _message;

			public HttpBasicAuthenticationRequest(
				BasicHttpAuthenticationProvider basicHttpAuthenticationProvider, IncomingHttpRequestMessage message,
				string name, string suppliedPassword)
				: base(name, suppliedPassword) {
				_basicHttpAuthenticationProvider = basicHttpAuthenticationProvider;
				_message = message;
			}

			public override void Unauthorized() {
				ReplyUnauthorized(_message.Entity);
			}

			public override void Authenticated(IPrincipal principal) {
				_basicHttpAuthenticationProvider.Authenticated(_message, principal);
			}

			public override void Error() {
				ReplyInternalServerError(_message.Entity);
			}

			public override void NotReady() {
				ReplyNotYetAvailable(_message.Entity);
			}
		}
	}
}
