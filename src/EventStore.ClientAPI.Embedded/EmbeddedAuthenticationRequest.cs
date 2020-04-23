using System;
using System.Security.Claims;
using EventStore.ClientAPI.Exceptions;
using EventStore.Plugins.Authentication;

namespace EventStore.ClientAPI.Embedded {
	internal class EmbeddedAuthenticationRequest : AuthenticationRequest {
		private readonly Action<ClaimsPrincipal> _onAuthenticated;
		private readonly Action<Exception> _setException;

		internal EmbeddedAuthenticationRequest(
			string name, string suppliedPassword, Action<Exception> setException, Action<ClaimsPrincipal> onAuthenticated)
			: base("embedded", name, suppliedPassword) {
			_onAuthenticated = onAuthenticated;
			_setException = setException;
		}

		public override void Unauthorized() {
			_setException(new NotAuthenticatedException());
		}

		public override void Authenticated(ClaimsPrincipal principal) {
			_onAuthenticated(principal);
		}

		public override void Error() {
			_setException(new ServerErrorException());
		}

		public override void NotReady() {
			_setException(new ServerErrorException("The server is not ready."));
		}
	}
}
