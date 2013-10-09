using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Transport.Http.Authentication;
using EventStore.Core.Settings;

namespace EventStore.Core.Authentication
{
	public class InternalAuthenticationProviderFactory : IAuthenticationProviderFactory
	{
		public IAuthenticationProvider BuildAuthenticationProvider(IPublisher mainQueue, IPublisher workersQueue, InMemoryBus[] workerBusses)
		{
			var passwordHashAlgorithm = new Rfc2898PasswordHashAlgorithm();
			var dispatcher = new IODispatcher(mainQueue, new PublishEnvelope(workersQueue, crossThread: true));
			
			foreach (var bus in workerBusses) {
				bus.Subscribe(dispatcher.ForwardReader);
				bus.Subscribe(dispatcher.BackwardReader);
				bus.Subscribe(dispatcher.Writer);
				bus.Subscribe(dispatcher.StreamDeleter);
				bus.Subscribe(dispatcher);
			}
			
			return new InternalAuthenticationProvider(dispatcher, passwordHashAlgorithm, ESConsts.CachedPrincipalCount);
		}
	}
}
