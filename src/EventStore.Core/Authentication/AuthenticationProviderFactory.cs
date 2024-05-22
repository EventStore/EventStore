using System;
using EventStore.Plugins.Authentication;

namespace EventStore.Core.Authentication;

public class AuthenticationProviderFactory(Func<AuthenticationProviderFactoryComponents, IAuthenticationProviderFactory> providerFactory) {
	public IAuthenticationProviderFactory GetFactory(AuthenticationProviderFactoryComponents components) => providerFactory(components);
}