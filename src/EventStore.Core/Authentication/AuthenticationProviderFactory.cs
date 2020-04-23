using System;
using EventStore.Plugins.Authentication;

namespace EventStore.Core.Authentication {
	public class AuthenticationProviderFactory {
		private readonly Func<AuthenticationProviderFactoryComponents, IAuthenticationProviderFactory>
			_authenticationProviderFactory;

		public AuthenticationProviderFactory(
			Func<AuthenticationProviderFactoryComponents, IAuthenticationProviderFactory>
				authenticationProviderFactory) {
			_authenticationProviderFactory = authenticationProviderFactory;
		}

		public IAuthenticationProviderFactory GetFactory(
			AuthenticationProviderFactoryComponents authenticationProviderFactoryComponents) =>
			_authenticationProviderFactory(authenticationProviderFactoryComponents);
	}
}
