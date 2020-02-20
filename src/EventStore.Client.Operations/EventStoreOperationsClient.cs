using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using EventStore.Client.Interceptors;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;

namespace EventStore.Client.Operations {
	public partial class EventStoreOperationsClient : IDisposable {
		private readonly GrpcChannel _channel;
		private readonly Operations.OperationsClient _client;

		public EventStoreOperationsClient(EventStoreClientSettings settings = null) {
			settings ??= new EventStoreClientSettings();
			var httpHandler = settings.CreateHttpMessageHandler?.Invoke() ?? new HttpClientHandler();

			_channel = GrpcChannel.ForAddress(settings.ConnectivitySettings.Address ?? new UriBuilder {
				Scheme = Uri.UriSchemeHttps,
				Port = 2113
			}.Uri, new GrpcChannelOptions {
				HttpClient = new HttpClient(httpHandler) {
					Timeout = Timeout.InfiniteTimeSpan,
					DefaultRequestVersion = new Version(2, 0),
				},
				LoggerFactory = settings.LoggerFactory
			});
			var connectionName = settings.ConnectionName ?? $"ES-{Guid.NewGuid()}";

			var callInvoker = (settings.Interceptors ?? Array.Empty<Interceptor>()).Aggregate(
				_channel.CreateCallInvoker()
					.Intercept(new TypedExceptionInterceptor())
					.Intercept(new ConnectionNameInterceptor(connectionName)),
				(invoker, interceptor) => invoker.Intercept(interceptor));
			_client = new Operations.OperationsClient(callInvoker);
		}

		public EventStoreOperationsClient(Uri address, Func<HttpMessageHandler> createHttpMessageHandler = default)
			: this(
				new EventStoreClientSettings {
					ConnectivitySettings = {
						Address = address
					},
					CreateHttpMessageHandler = createHttpMessageHandler
				}) {
		}

		public void Dispose() => _channel?.Dispose();
	}
}
