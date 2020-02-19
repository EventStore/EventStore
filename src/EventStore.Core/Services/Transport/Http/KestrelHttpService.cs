using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Services.Transport.Http.Messages;
using EventStore.Core.Settings;
using EventStore.Transport.Http.EntityManagement;
using EventStore.Core.Services.Transport.Http.Authentication;
using Microsoft.AspNetCore.Http;
using HttpStatusCode = EventStore.Transport.Http.HttpStatusCode;
using ILogger = Serilog.ILogger;
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
		private static readonly ILogger Log = Serilog.Log.ForContext<KestrelHttpService>();

		public ServiceAccessibility Accessibility => _accessibility;
		public bool IsListening => _isListening;
		public IEnumerable<IPEndPoint> EndPoints { get; }

		public IEnumerable<ControllerAction> Actions => _uriRouter.Actions;

		private readonly ServiceAccessibility _accessibility;
		private readonly IPublisher _inputBus;
		private readonly IUriRouter _uriRouter;
		private readonly IEnvelope _publishEnvelope;
		private readonly bool _logHttpRequests;
		private readonly MultiQueuedHandler _requestsMultiHandler;

		private readonly IPAddress _advertiseAsAddress;
		private readonly int _advertiseAsPort;
		private readonly bool _disableAuthorization;

		private bool _isListening;

		public KestrelHttpService(ServiceAccessibility accessibility, IPublisher inputBus, IUriRouter uriRouter,
			MultiQueuedHandler multiQueuedHandler, bool logHttpRequests, IPAddress advertiseAsAddress,
			int advertiseAsPort, bool disableAuthorization, params IPEndPoint[] endPoints) {
			Ensure.NotNull(inputBus, nameof(inputBus));
			Ensure.NotNull(uriRouter, nameof(uriRouter));
			Ensure.NotNull(endPoints, nameof(endPoints));

			_accessibility = accessibility;
			_inputBus = inputBus;
			_uriRouter = uriRouter;
			_publishEnvelope = new PublishEnvelope(inputBus);

			_requestsMultiHandler = multiQueuedHandler;
			_logHttpRequests = logHttpRequests;

			_advertiseAsAddress = advertiseAsAddress;
			_advertiseAsPort = advertiseAsPort;

			_disableAuthorization = disableAuthorization;

			EndPoints = endPoints;
		}

		public void Handle(SystemMessage.SystemInit message) {
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
					$"HttpServer [{String.Join(", ", EndPoints)}]"));
		}

		public void Handle(HttpMessage.PurgeTimedOutRequests message) {
			if (_accessibility != message.Accessibility)
				return;

			_requestsMultiHandler.PublishToAll(message);

			_inputBus.Publish(
				TimerMessage.Schedule.Create(
					UpdateInterval, _publishEnvelope, new HttpMessage.PurgeTimedOutRequests(_accessibility)));
		}

		private Task RequestReceived(HttpContext context) {
			var tcs = new TaskCompletionSource<bool>();
			var entity = new HttpEntity(new CoreHttpRequestAdapter(context.Request),
				new CoreHttpResponseAdapter(context.Response), context.User, _logHttpRequests,
				_advertiseAsAddress, _advertiseAsPort, () => tcs.TrySetResult(true));
			entity.SetUser(context.User);
			_requestsMultiHandler.Handle(new AuthenticatedHttpRequestMessage(this, entity));
			return tcs.Task;
		}

		public void Shutdown() {
			_isListening = false;
		}

		public RequestDelegate AppFunc => RequestReceived;

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
					
					man.ReplyStatus(HttpStatusCode.Unauthorized, "Unauthorized", (exc) => {
						Log.Debug("Error while sending reply (http service): {exc}.", exc.Message);
					});
				}

				return new RequestParams(ESConsts.HttpTimeout);
			});
		}

		private static bool Authorized(ClaimsPrincipal user, AuthorizationLevel requiredAuthorizationLevel) =>
			requiredAuthorizationLevel switch {
				AuthorizationLevel.None => true,
				AuthorizationLevel.User => (user != null && !user.HasClaim(ClaimTypes.Anonymous, "")),
				AuthorizationLevel.Ops => (user != null &&
				                           (user.LegacyRoleCheck(SystemRoles.Admins) ||
				                            user.LegacyRoleCheck(SystemRoles.Operations))),
				AuthorizationLevel.Admin => (user != null && user.LegacyRoleCheck(SystemRoles.Admins)),
				_ => false
			};

		public List<UriToActionMatch> GetAllUriMatches(Uri uri) => _uriRouter.GetAllUriMatches(uri);

		public static void CreateAndSubscribePipeline(IBus bus) {
			Ensure.NotNull(bus, "bus");

			var requestProcessor = new AuthenticatedHttpRequestProcessor();
			bus.Subscribe<AuthenticatedHttpRequestMessage>(requestProcessor);
			bus.Subscribe<HttpMessage.PurgeTimedOutRequests>(requestProcessor);
		}
	}
}
