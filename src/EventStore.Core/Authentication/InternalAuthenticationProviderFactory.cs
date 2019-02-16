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
		public IAuthenticationProvider BuildAuthenticationProvider(IPublisher mainQueue, ISubscriber mainBus,
			IPublisher workersQueue, InMemoryBus[] workerBusses) {
			var passwordHashAlgorithm = new Rfc2898PasswordHashAlgorithm();
			var dispatcher = new IODispatcher(mainQueue, new PublishEnvelope(workersQueue, crossThread: true));

			foreach (var bus in workerBusses) {
				bus.Subscribe(dispatcher.ForwardReader);
				bus.Subscribe(dispatcher.BackwardReader);
				bus.Subscribe(dispatcher.Writer);
				bus.Subscribe(dispatcher.StreamDeleter);
				bus.Subscribe(dispatcher.Awaker);
				bus.Subscribe(dispatcher);
			}

			// USER MANAGEMENT
			var ioDispatcher = new IODispatcher(mainQueue, new PublishEnvelope(mainQueue));
			mainBus.Subscribe(ioDispatcher.BackwardReader);
			mainBus.Subscribe(ioDispatcher.ForwardReader);
			mainBus.Subscribe(ioDispatcher.Writer);
			mainBus.Subscribe(ioDispatcher.StreamDeleter);
			mainBus.Subscribe(ioDispatcher.Awaker);
			mainBus.Subscribe(ioDispatcher);

			var userManagement = new UserManagementService(mainQueue, ioDispatcher, passwordHashAlgorithm,
				skipInitializeStandardUsersCheck: false);
			mainBus.Subscribe<UserManagementMessage.Create>(userManagement);
			mainBus.Subscribe<UserManagementMessage.Update>(userManagement);
			mainBus.Subscribe<UserManagementMessage.Enable>(userManagement);
			mainBus.Subscribe<UserManagementMessage.Disable>(userManagement);
			mainBus.Subscribe<UserManagementMessage.Delete>(userManagement);
			mainBus.Subscribe<UserManagementMessage.ResetPassword>(userManagement);
			mainBus.Subscribe<UserManagementMessage.ChangePassword>(userManagement);
			mainBus.Subscribe<UserManagementMessage.Get>(userManagement);
			mainBus.Subscribe<UserManagementMessage.GetAll>(userManagement);
			mainBus.Subscribe<SystemMessage.BecomeMaster>(userManagement);

			var provider =
				new InternalAuthenticationProvider(dispatcher, passwordHashAlgorithm, ESConsts.CachedPrincipalCount);
			var passwordChangeNotificationReader = new PasswordChangeNotificationReader(mainQueue, dispatcher);
			mainBus.Subscribe<SystemMessage.SystemStart>(passwordChangeNotificationReader);
			mainBus.Subscribe<SystemMessage.BecomeShutdown>(passwordChangeNotificationReader);
			mainBus.Subscribe(provider);
			return provider;
		}

		public void RegisterHttpControllers(HttpService externalHttpService, HttpService internalHttpService,
			HttpSendService httpSendService, IPublisher mainQueue, IPublisher networkSendQueue) {
			var usersController = new UsersController(httpSendService, mainQueue, networkSendQueue);
			externalHttpService.SetupController(usersController);
			if (internalHttpService != null) {
				internalHttpService.SetupController(usersController);
			}
		}
	}
}
