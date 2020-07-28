using EventStore.Plugins.Authentication;
using Serilog;

namespace EventStore.Core.Authentication.InternalAuthentication {
	public class PassthroughAuthenticationProviderFactory : IAuthenticationProviderFactory {
		public IAuthenticationProvider Build(bool logFailedAuthenticationAttempts, ILogger logger) =>
			new PassthroughAuthenticationProvider();
	}
}
