using EventStore.Core.Bus;

namespace EventStore.Core.Authorization {
	public class AuthorizationProviderFactoryComponents {
		public IPublisher MainQueue { get; set; }
		public ISubscriber MainBus { get; set; }
	}
}
