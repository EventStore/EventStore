using System;
using System.Security.Principal;
using EventStore.Core.Authentication;
using EventStore.Core.Helpers;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Helpers;

namespace EventStore.Core.Tests.Authentication {
	public class with_internal_authentication_provider : TestFixtureWithExistingEvents {
		protected new IODispatcher _ioDispatcher;
		protected InternalAuthenticationProvider _internalAuthenticationProvider;

		protected void SetUpProvider() {
			_ioDispatcher = new IODispatcher(_bus, new PublishEnvelope(_bus));
			_bus.Subscribe(_ioDispatcher.BackwardReader);
			_bus.Subscribe(_ioDispatcher.ForwardReader);
			_bus.Subscribe(_ioDispatcher.Writer);
			_bus.Subscribe(_ioDispatcher.StreamDeleter);
			_bus.Subscribe(_ioDispatcher.Awaker);
			_bus.Subscribe(_ioDispatcher);

			PasswordHashAlgorithm passwordHashAlgorithm = new StubPasswordHashAlgorithm();
			_internalAuthenticationProvider =
				new InternalAuthenticationProvider(_ioDispatcher, passwordHashAlgorithm, 1000);
			_bus.Subscribe(_internalAuthenticationProvider);
		}
	}

	class TestAuthenticationRequest : AuthenticationRequest {
		private readonly Action _unauthorized;
		private readonly Action<IPrincipal> _authenticated;
		private readonly Action _error;
		private readonly Action _notReady;

		public TestAuthenticationRequest(string name, string suppliedPassword, Action unauthorized,
			Action<IPrincipal> authenticated, Action error, Action notReady)
			: base(name, suppliedPassword) {
			_unauthorized = unauthorized;
			_authenticated = authenticated;
			_error = error;
			_notReady = notReady;
		}

		public override void Unauthorized() {
			_unauthorized();
		}

		public override void Authenticated(IPrincipal principal) {
			_authenticated(principal);
		}

		public override void Error() {
			_error();
		}

		public override void NotReady() {
			_notReady();
		}
	}
}
