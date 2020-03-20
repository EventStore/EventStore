using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services;
using EventStore.Core.Services.Transport.Http;
using EventStore.Core.Services.Transport.Http.Authentication;
using EventStore.Core.Services.Transport.Http.Controllers;
using EventStore.Core.Services.UserManagement;
using EventStore.Core.Settings;

namespace EventStore.Core.Authentication {
	public class InternalAuthenticationProviderFactory : IAuthenticationProviderFactory {
		private readonly AuthenticationProviderFactoryComponents _components;
		private readonly IODispatcher _dispatcher;
		private readonly Rfc2898PasswordHashAlgorithm _passwordHashAlgorithm;

		public InternalAuthenticationProviderFactory(AuthenticationProviderFactoryComponents components) {
			_components = components;
			_passwordHashAlgorithm = new Rfc2898PasswordHashAlgorithm();
			_dispatcher = new IODispatcher(components.MainQueue, new PublishEnvelope(components.WorkersQueue, crossThread: true));

			foreach (var bus in components.WorkerBuses) {
				bus.Subscribe(_dispatcher.ForwardReader);
				bus.Subscribe(_dispatcher.BackwardReader);
				bus.Subscribe(_dispatcher.Writer);
				bus.Subscribe(_dispatcher.StreamDeleter);
				bus.Subscribe(_dispatcher.Awaker);
				bus.Subscribe(_dispatcher);
			}

			// USER MANAGEMENT
			var ioDispatcher = new IODispatcher(components.MainQueue, new PublishEnvelope(components.MainQueue));
			components.MainBus.Subscribe(ioDispatcher.BackwardReader);
			components.MainBus.Subscribe(ioDispatcher.ForwardReader);
			components.MainBus.Subscribe(ioDispatcher.Writer);
			components.MainBus.Subscribe(ioDispatcher.StreamDeleter);
			components.MainBus.Subscribe(ioDispatcher.Awaker);
			components.MainBus.Subscribe(ioDispatcher);

			var userManagement = new UserManagementService(components.MainQueue, ioDispatcher, _passwordHashAlgorithm,
				skipInitializeStandardUsersCheck: false);
			components.MainBus.Subscribe<UserManagementMessage.Create>(userManagement);
			components.MainBus.Subscribe<UserManagementMessage.Update>(userManagement);
			components.MainBus.Subscribe<UserManagementMessage.Enable>(userManagement);
			components.MainBus.Subscribe<UserManagementMessage.Disable>(userManagement);
			components.MainBus.Subscribe<UserManagementMessage.Delete>(userManagement);
			components.MainBus.Subscribe<UserManagementMessage.ResetPassword>(userManagement);
			components.MainBus.Subscribe<UserManagementMessage.ChangePassword>(userManagement);
			components.MainBus.Subscribe<UserManagementMessage.Get>(userManagement);
			components.MainBus.Subscribe<UserManagementMessage.GetAll>(userManagement);
			components.MainBus.Subscribe<SystemMessage.BecomeLeader>(userManagement);
			components.MainBus.Subscribe<SystemMessage.BecomeFollower>(userManagement);
			
			var usersController = new UsersController(components.HttpSendService, components.MainQueue, components.WorkersQueue);
			components.ExternalHttpService.SetupController(usersController);
		}

		public IAuthenticationProvider BuildAuthenticationProvider(bool logFailedAuthenticationAttempts) {
			var provider =
				new InternalAuthenticationProvider(_dispatcher, _passwordHashAlgorithm, ESConsts.CachedPrincipalCount,
					logFailedAuthenticationAttempts);
			var passwordChangeNotificationReader = new PasswordChangeNotificationReader(_components.MainQueue, _dispatcher);
			_components.MainBus.Subscribe<SystemMessage.SystemStart>(passwordChangeNotificationReader);
			_components.MainBus.Subscribe<SystemMessage.BecomeShutdown>(passwordChangeNotificationReader);
			_components.MainBus.Subscribe(provider);
			return provider;
		}
	}
}
