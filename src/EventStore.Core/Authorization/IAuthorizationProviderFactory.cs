using EventStore.Core.Bus;

namespace EventStore.Core.Authorization {
	public interface IAuthorizationProviderFactory {
		IAuthorizationProvider Build(IPublisher mainQueue);
	}
}
