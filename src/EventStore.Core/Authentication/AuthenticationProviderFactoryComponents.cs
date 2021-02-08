using EventStore.Core.Bus;
using EventStore.Core.Services;
using EventStore.Core.Services.Transport.Http;

namespace EventStore.Core.Authentication {
	public class AuthenticationProviderFactoryComponents {
		public IPublisher MainQueue { get; set; }
		public ISubscriber MainBus { get; set; }
		public IPublisher WorkersQueue { get; set; }
		public InMemoryBus[] WorkerBuses { get; set; }
		public HttpSendService HttpSendService { get; set; }
		public IHttpService HttpService { get; set; }
	}
}
