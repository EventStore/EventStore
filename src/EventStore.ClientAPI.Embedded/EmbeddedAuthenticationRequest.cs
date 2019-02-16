using System;
using System.Security.Principal;
using EventStore.ClientAPI.Exceptions;
using EventStore.Core.Authentication;

namespace EventStore.ClientAPI.Embedded {
	internal class EmbeddedAuthenticationRequest : AuthenticationRequest {
		private readonly Action<IPrincipal> _onAuthenticated;
		private readonly Action<Exception> _setException;

		internal EmbeddedAuthenticationRequest(
			string name, string suppliedPassword, Action<Exception> setException, Action<IPrincipal> onAuthenticated)
			: base(name, suppliedPassword) {
			_onAuthenticated = onAuthenticated;
			_setException = setException;
		}

		public override void Unauthorized() {
			_setException(new NotAuthenticatedException());
		}

		public override void Authenticated(IPrincipal principal) {
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
