using System;
using System.Collections.Generic;
using System.Net;
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

namespace EventStore.Core.Services.Transport.Http {
	public class HttpService : IHttpService,
		IHandle<SystemMessage.SystemInit>,
		IHandle<SystemMessage.BecomeShuttingDown>,
		IHandle<HttpMessage.PurgeTimedOutRequests> {
		private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

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

		public HttpService(ServiceAccessibility accessibility, IPublisher inputBus, IUriRouter uriRouter,
			MultiQueuedHandler multiQueuedHandler, bool logHttpRequests, IPAddress advertiseAsAddress,
			int advertiseAsPort, params string[] prefixes) {
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
		}

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
			var entity = new HttpEntity(context.Request, context.Response, context.User, _logHttpRequests,
				_advertiseAsAddress, _advertiseAsPort);
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
				handler(man, match);
				return new RequestParams(ESConsts.HttpTimeout);
			});
		}

		public List<UriToActionMatch> GetAllUriMatches(Uri uri) {
			return _uriRouter.GetAllUriMatches(uri);
		}
	}
}
