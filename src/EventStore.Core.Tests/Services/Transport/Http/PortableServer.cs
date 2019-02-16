using System;
using System.Net;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Transport.Http;
using EventStore.Core.Services.Transport.Http.Authentication;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Client;

namespace EventStore.Core.Tests.Services.Transport.Http {
	public class PortableServer {
		public bool IsListening {
			get { return _service.IsListening; }
		}

		public IHttpClient BuiltInClient {
			get { return _client; }
		}

		private InMemoryBus _bus;
		private HttpService _service;
		private MultiQueuedHandler _multiQueuedHandler;
		private HttpAsyncClient _client;
		private TimeSpan _timeout;

		private readonly IPEndPoint _serverEndPoint;

		public PortableServer(IPEndPoint serverEndPoint, int timeout = 10000) {
			Ensure.NotNull(serverEndPoint, "serverEndPoint");
			_serverEndPoint = serverEndPoint;
			_timeout = TimeSpan.FromMilliseconds(timeout);
		}

		public void SetUp() {
			_bus = new InMemoryBus(string.Format("bus_{0}", _serverEndPoint.Port));

			{
				var pipelineBus = InMemoryBus.CreateTest();
				var queue = new QueuedHandlerThreadPool(pipelineBus, "Test", true, TimeSpan.FromMilliseconds(50));
				_multiQueuedHandler = new MultiQueuedHandler(new IQueuedHandler[] {queue}, null);
				_multiQueuedHandler.Start();
				var httpAuthenticationProviders = new HttpAuthenticationProvider[]
					{new AnonymousHttpAuthenticationProvider()};

				_service = new HttpService(ServiceAccessibility.Private, _bus, new NaiveUriRouter(),
					_multiQueuedHandler, false, null, 0, _serverEndPoint.ToHttpUrl(EndpointExtensions.HTTP_SCHEMA));
				HttpService.CreateAndSubscribePipeline(pipelineBus, httpAuthenticationProviders);
				_client = new HttpAsyncClient(_timeout);
			}

			HttpBootstrap.Subscribe(_bus, _service);
		}

		public void TearDown() {
			HttpBootstrap.Unsubscribe(_bus, _service);

			_service.Shutdown();
			_client.Dispose();
			_multiQueuedHandler.Stop();
		}

		public void Publish(Message message) {
			_bus.Publish(message);
		}

		public Tuple<bool, string> StartServiceAndSendRequest(Action<HttpService> bootstrap,
			string requestUrl,
			Func<HttpResponse, bool> verifyResponse) {
			_bus.Publish(new SystemMessage.SystemInit());

			bootstrap(_service);

			var signal = new AutoResetEvent(false);
			var success = false;
			var error = string.Empty;

			_client.Get(requestUrl,
				response => {
					success = verifyResponse(response);
					signal.Set();
				},
				exception => {
					success = false;
					error = exception.Message;
					signal.Set();
				});

			signal.WaitOne();
			return new Tuple<bool, string>(success, error);
		}
	}
}
