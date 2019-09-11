using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Principal;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Services.Transport.Http.Authentication;
using EventStore.Core.Services.Transport.Http.Messages;
using EventStore.Core.Settings;
using EventStore.Transport.Http.EntityManagement;
using EventStore.Transport.Http.Server;
using EventStore.Common.Log;

namespace EventStore.Core.Services.Transport.Http {
	public class HttpService : IHttpService,
		IHandle<SystemMessage.SystemInit>,
		IHandle<SystemMessage.BecomeShuttingDown>,
		IHandle<HttpMessage.PurgeTimedOutRequests> {
		private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);
		private static readonly ILogger Log = LogManager.GetLoggerFor<HttpService>();
		public bool IsListening {
			get { return _server.IsListening; }
		}

		public IEnumerable<string> ListenPrefixes {
			get { return _server._listenPrefixes; }
		}

		public ServiceAccessibility Accessibility {
			get { return _accessibility; }
		}

		private readonly ServiceAccessibility _accessibility;
		private readonly IPublisher _inputBus;
		private readonly IUriRouter _uriRouter;
		private readonly IEnvelope _publishEnvelope;
		private readonly bool _logHttpRequests;

		private readonly HttpAsyncServer _server;
		private readonly MultiQueuedHandler _requestsMultiHandler;

		private IPAddress _advertiseAsAddress;
		private int _advertiseAsPort;
		private bool _disableAuthorization;

		public HttpService(ServiceAccessibility accessibility, IPublisher inputBus, IUriRouter uriRouter,
			MultiQueuedHandler multiQueuedHandler, bool logHttpRequests, IPAddress advertiseAsAddress,
			int advertiseAsPort, bool disableAuthorization, params string[] prefixes) {
			Ensure.NotNull(inputBus, "inputBus");
			Ensure.NotNull(uriRouter, "uriRouter");
			Ensure.NotNull(prefixes, "prefixes");

			_accessibility = accessibility;
			_inputBus = inputBus;
			_uriRouter = uriRouter;
			_publishEnvelope = new PublishEnvelope(inputBus);

			_requestsMultiHandler = multiQueuedHandler;
			_logHttpRequests = logHttpRequests;

			_server = new HttpAsyncServer(prefixes);
			_server.RequestReceived += RequestReceived;

			_advertiseAsAddress = advertiseAsAddress;
			_advertiseAsPort = advertiseAsPort;

			_disableAuthorization = disableAuthorization;
		}

		// TODO: JPB this doesn't really belong here but no idea where to move it to...
		public static void CreateAndSubscribePipeline(IBus bus,
			HttpAuthenticationProvider[] httpAuthenticationProviders) {
			Ensure.NotNull(bus, "bus");
			Ensure.NotNull(httpAuthenticationProviders, "httpAuthenticationProviders");

			// ReSharper disable RedundantTypeArgumentsOfMethod
			var requestAuthenticationManager =
				new IncomingHttpRequestAuthenticationManager(httpAuthenticationProviders);
			bus.Subscribe<IncomingHttpRequestMessage>(requestAuthenticationManager);
			// ReSharper restore RedundantTypeArgumentsOfMethod

			var requestProcessor = new AuthenticatedHttpRequestProcessor();
			bus.Subscribe<AuthenticatedHttpRequestMessage>(requestProcessor);
			bus.Subscribe<HttpMessage.PurgeTimedOutRequests>(requestProcessor);
		}

		public void Handle(SystemMessage.SystemInit message) {
			if (_server.TryStart()) {
				_inputBus.Publish(
					TimerMessage.Schedule.Create(
						UpdateInterval, _publishEnvelope, new HttpMessage.PurgeTimedOutRequests(_accessibility)));
			} else {
				Application.Exit(ExitCode.Error,
					string.Format("HTTP async server failed to start listening at [{0}].",
						string.Join(", ", _server._listenPrefixes)));
			}
		}

		public void Handle(SystemMessage.BecomeShuttingDown message) {
			if (message.ShutdownHttp)
				Shutdown();
			_inputBus.Publish(
				new SystemMessage.ServiceShutdown(
					string.Format("HttpServer [{0}]", string.Join(", ", _server._listenPrefixes))));
		}

		private void RequestReceived(HttpAsyncServer sender, HttpListenerContext context) {
			var request = new HttpListenerRequestAdapter(context.Request);
			var response = new HttpListenerResponseAdapter(context.Response);
			var entity = new HttpEntity(request,
				response, context.User, _logHttpRequests,
				_advertiseAsAddress, _advertiseAsPort, () => {
					response.OutputStream.Close();
					response.Close();
				});
			_requestsMultiHandler.Handle(new IncomingHttpRequestMessage(this, entity, _requestsMultiHandler));
		}

		public void Handle(HttpMessage.PurgeTimedOutRequests message) {
			if (_accessibility != message.Accessibility)
				return;

			_requestsMultiHandler.PublishToAll(message);

			_inputBus.Publish(
				TimerMessage.Schedule.Create(
					UpdateInterval, _publishEnvelope, new HttpMessage.PurgeTimedOutRequests(_accessibility)));
		}

		public void Shutdown() {
			_server.Shutdown();
		}

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

		public List<UriToActionMatch> GetAllUriMatches(Uri uri) {
			return _uriRouter.GetAllUriMatches(uri);
		}
	}
}
