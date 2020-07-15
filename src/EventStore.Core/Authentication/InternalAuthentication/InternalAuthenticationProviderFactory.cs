using EventStore.Common.Settings;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Transport.Http.Authentication;
using EventStore.Core.Services.Transport.Http.Controllers;
using EventStore.Core.Settings;
using EventStore.Plugins.Authentication;
using Serilog;

namespace EventStore.Core.Authentication.InternalAuthentication {
	public class InternalAuthenticationProviderFactory : IAuthenticationProviderFactory {
		private readonly AuthenticationProviderFactoryComponents _components;
		private readonly IODispatcher _dispatcher;
		private readonly Rfc2898PasswordHashAlgorithm _passwordHashAlgorithm;

		public InternalAuthenticationProviderFactory(AuthenticationProviderFactoryComponents components) {
			_components = components;
			_passwordHashAlgorithm = new Rfc2898PasswordHashAlgorithm();
			_dispatcher = new IODispatcher(components.MainQueue,
				new PublishEnvelope(components.WorkersQueue, crossThread: true));

			foreach (var bus in components.WorkerBuses) {
				bus.Subscribe(_dispatcher.ForwardReader);
				bus.Subscribe(_dispatcher.BackwardReader);
				bus.Subscribe(_dispatcher.Writer);
				bus.Subscribe(_dispatcher.StreamDeleter);
				bus.Subscribe(_dispatcher.Awaker);
				bus.Subscribe(_dispatcher);
			}

			var usersController =
				new UsersController(components.HttpSendService, components.MainQueue, components.WorkersQueue);
			components.HttpService.SetupController(usersController);
		}

		public IAuthenticationProvider Build(bool logFailedAuthenticationAttempts, ILogger logger) {
			var provider =
				new InternalAuthenticationProvider(_components.MainBus, _dispatcher, _passwordHashAlgorithm, ESConsts.CachedPrincipalCount,
					logFailedAuthenticationAttempts);
			var passwordChangeNotificationReader =
				new PasswordChangeNotificationReader(_components.MainQueue, _dispatcher);
			_components.MainBus.Subscribe<SystemMessage.SystemStart>(passwordChangeNotificationReader);
			_components.MainBus.Subscribe<SystemMessage.BecomeShutdown>(passwordChangeNotificationReader);
			_components.MainBus.Subscribe(provider);

			var ioDispatcher = new IODispatcher(_components.MainQueue, new PublishEnvelope(_components.MainQueue));
			_components.MainBus.Subscribe(ioDispatcher.BackwardReader);
			_components.MainBus.Subscribe(ioDispatcher.ForwardReader);
			_components.MainBus.Subscribe(ioDispatcher.Writer);
			_components.MainBus.Subscribe(ioDispatcher.StreamDeleter);
			_components.MainBus.Subscribe(ioDispatcher.Awaker);
			_components.MainBus.Subscribe(ioDispatcher);

			return provider;
		}
	}
}
