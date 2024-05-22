using EventStore.Plugins.Authentication;
using Microsoft.Extensions.Logging;

namespace EventStore.Core.Authentication.PassthroughAuthentication;

public class PassthroughAuthenticationProviderFactory : IAuthenticationProviderFactory {
	public IAuthenticationProvider Build(bool logFailedAuthenticationAttempts, ILogger logger) => new PassthroughAuthenticationProvider();
}