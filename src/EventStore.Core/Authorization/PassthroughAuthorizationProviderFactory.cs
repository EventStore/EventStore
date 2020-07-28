using EventStore.Plugins.Authorization;

namespace EventStore.Core.Authorization {
	public class PassthroughAuthorizationProviderFactory : IAuthorizationProviderFactory {
		public IAuthorizationProvider Build() => new PassthroughAuthorizationProvider();
	}
}
