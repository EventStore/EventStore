using EventStore.Plugins.Authentication;

namespace EventStore.Core.Authentication.PassthroughAuthentication;

public class PassthroughAuthenticationProviderFactory : IAuthenticationProviderFactory {
	public IAuthenticationProvider Build(bool logFailedAuthenticationAttempts) => new PassthroughAuthenticationProvider();
}
