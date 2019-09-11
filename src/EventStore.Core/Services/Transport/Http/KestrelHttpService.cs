using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Principal;
using System.Threading.Tasks;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Services.Transport.Http.Messages;
using EventStore.Core.Settings;
using EventStore.Transport.Http.EntityManagement;
using EventStore.Common.Log;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MidFunc = System.Func<
	Microsoft.AspNetCore.Http.HttpContext,
	System.Func<System.Threading.Tasks.Task>,
	System.Threading.Tasks.Task
>;


namespace EventStore.Core.Services.Transport.Http {
	public class KestrelHttpService : IHttpService,
		IHandle<SystemMessage.SystemInit>,
		IHandle<SystemMessage.BecomeShuttingDown>,
		IHandle<HttpMessage.PurgeTimedOutRequests> {
		private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);
		private static readonly ILogger Log = LogManager.GetLoggerFor<KestrelHttpService>();

		public IEnumerable<string> ListenPrefixes { get; }
		public ServiceAccessibility Accessibility => _accessibility;
		public bool IsListening => _isListening;

		private readonly ServiceAccessibility _accessibility;
		private readonly IPublisher _inputBus;
		private readonly IUriRouter _uriRouter;
		private readonly IEnvelope _publishEnvelope;
		private readonly bool _logHttpRequests;

		private readonly MultiQueuedHandler _requestsMultiHandler;

		private readonly IPAddress _advertiseAsAddress;
		private readonly int _advertiseAsPort;
		private readonly bool _disableAuthorization;
		private readonly IWebHost _host;

		private bool _isListening;

		public KestrelHttpService(ServiceAccessibility accessibility, IPublisher inputBus, IUriRouter uriRouter,
			MultiQueuedHandler multiQueuedHandler, bool logHttpRequests, IPAddress advertiseAsAddress,
			int advertiseAsPort, bool disableAuthorization, params string[] prefixes) {
			Ensure.NotNull(inputBus, nameof(inputBus));
			Ensure.NotNull(uriRouter, nameof(uriRouter));
			Ensure.NotNull(prefixes, nameof(prefixes));

			_accessibility = accessibility;
			_inputBus = inputBus;
			_uriRouter = uriRouter;
			_publishEnvelope = new PublishEnvelope(inputBus);

			_requestsMultiHandler = multiQueuedHandler;
			_logHttpRequests = logHttpRequests;

			_advertiseAsAddress = advertiseAsAddress;
			_advertiseAsPort = advertiseAsPort;

			_disableAuthorization = disableAuthorization;
			ListenPrefixes = prefixes;

			_host = new WebHostBuilder()
				.UseStartup(new EventStoreStartup(this))
				.UseKestrel()
				.UseUrls(prefixes)
				.Build();
		}

		public void Handle(SystemMessage.SystemInit message) {
			try {
				_host.Start();
			} catch (Exception ex) {
				Application.Exit(ExitCode.Error,
					$"HTTP async server failed to start listening at [{string.Join(", ", ListenPrefixes)}].");
				return;
			}

			_inputBus.Publish(
				TimerMessage.Schedule.Create(
					UpdateInterval, _publishEnvelope, new HttpMessage.PurgeTimedOutRequests(_accessibility)));
			_isListening = true;
		}

		public void Handle(SystemMessage.BecomeShuttingDown message) {
			if (message.ShutdownHttp)
				Shutdown();
			_inputBus.Publish(
				new SystemMessage.ServiceShutdown(
					$"HttpServer [{string.Join(", ", ListenPrefixes)}]"));
		}

		public void Handle(HttpMessage.PurgeTimedOutRequests message) {
			if (_accessibility != message.Accessibility)
				return;

			_requestsMultiHandler.PublishToAll(message);

			_inputBus.Publish(
				TimerMessage.Schedule.Create(
					UpdateInterval, _publishEnvelope, new HttpMessage.PurgeTimedOutRequests(_accessibility)));
		}

		private Task RequestReceived(HttpContext context, Func<Task> next) {
			var tcs = new TaskCompletionSource<bool>();
			var entity = new HttpEntity(new CoreHttpRequestAdapter(context.Request),
				new CoreHttpResponseAdapter(context.Response), context.User, _logHttpRequests,
				_advertiseAsAddress, _advertiseAsPort, () => tcs.TrySetResult(true));
			_requestsMultiHandler.Handle(new IncomingHttpRequestMessage(this, entity, _requestsMultiHandler));
			return tcs.Task;
		}

		public void Shutdown() {
			_isListening = false;
		}

		public MidFunc MidFunc => RequestReceived;

		public void SetupController(IHttpController controller) {
			Ensure.NotNull(controller, "controller");
			controller.Subscribe(this);
		}

		public void RegisterCustomAction(ControllerAction action,
			Func<HttpEntityManager, UriTemplateMatch, RequestParams> handler) {
			Ensure.NotNull(action, "action");
			Ensure.NotNull(handler, "handler");

			_uriRouter.RegisterAction(action, handler);
		}

		public void RegisterAction(ControllerAction action, Action<HttpEntityManager, UriTemplateMatch> handler) {
			Ensure.NotNull(action, "action");
			Ensure.NotNull(handler, "handler");

			_uriRouter.RegisterAction(action, (man, match) => {
				if (_disableAuthorization || Authorized(man.User, action.RequiredAuthorizationLevel)) {
					handler(man, match);
				} else {
					man.ReplyStatus(EventStore.Transport.Http.HttpStatusCode.Unauthorized, "Unauthorized", (exc) => {
						Log.Debug("Error while sending reply (http service): {exc}.", exc.Message);
					});
				}

				return new RequestParams(ESConsts.HttpTimeout);
			});
		}

		private bool Authorized(IPrincipal user, AuthorizationLevel requiredAuthorizationLevel) {
			switch (requiredAuthorizationLevel) {
				case AuthorizationLevel.None:
					return true;
				case AuthorizationLevel.User:
					return user != null;
				case AuthorizationLevel.Ops:
					return user != null && (user.IsInRole(SystemRoles.Admins) || user.IsInRole(SystemRoles.Operations));
				case AuthorizationLevel.Admin:
					return user != null && user.IsInRole(SystemRoles.Admins);
				default:
					return false;
			}
		}

		public List<UriToActionMatch> GetAllUriMatches(Uri uri) => _uriRouter.GetAllUriMatches(uri);

		private class EventStoreStartup : IStartup {
			private readonly KestrelHttpService _httpService;

			public EventStoreStartup(KestrelHttpService httpService) {
				_httpService = httpService;
			}

			public IServiceProvider ConfigureServices(IServiceCollection services) => services.BuildServiceProvider();

			public void Configure(IApplicationBuilder app) {
				app.Use(_httpService.RequestReceived);
				var lifetime = app.ApplicationServices.GetRequiredService<IApplicationLifetime>();

				lifetime.ApplicationStarted.Register(_httpService.OnStartup);
				lifetime.ApplicationStopping.Register(_httpService.OnShutdown);
			}
		}

	}
}
