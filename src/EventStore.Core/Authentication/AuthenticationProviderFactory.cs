using System;
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
		public IHttpService ExternalHttpService { get; set; }
		public IAuthenticationProviderPublisher Publisher { get; set; }
	}

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
