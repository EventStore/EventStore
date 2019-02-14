using EventStore.Core.Bus;
using EventStore.Core.Services;
using EventStore.Core.Services.Transport.Http;

namespace EventStore.Core.Authentication {
	public interface IAuthenticationProviderFactory {
		IAuthenticationProvider BuildAuthenticationProvider(IPublisher mainQueue, ISubscriber mainBus,
			IPublisher workersQueue, InMemoryBus[] workerBusses);

		void RegisterHttpControllers(HttpService externalHttpService, HttpService internalHttpService,
			HttpSendService httpSendService, IPublisher mainQueue, IPublisher networkSendQueue);
	}
}
