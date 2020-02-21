using System;
using System.Net.Http;
using System.Threading;
using EventStore.Core.Bus;
using Grpc.Net.Client;
using Serilog.Extensions.Logging;

namespace EventStore.Core.Cluster {
	public partial class EventStoreClusterClient : IDisposable {
		private readonly EventStore.Cluster.Cluster.ClusterClient _client;
		private readonly GrpcChannel _channel;
		private readonly IPublisher _bus;
		internal bool Disposed { get; private set; }

		public EventStoreClusterClient(Uri address, IPublisher bus,
			Func<HttpMessageHandler> httpMessageHandlerFactory = null) {
			_channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions {
				HttpClient = new HttpClient(httpMessageHandlerFactory?.Invoke() ?? new HttpClientHandler()) {
					Timeout = Timeout.InfiniteTimeSpan,
					DefaultRequestVersion = new Version(2, 0),
				},
				LoggerFactory = new SerilogLoggerFactory()
			});
			var callInvoker = _channel.CreateCallInvoker();
			_client = new EventStore.Cluster.Cluster.ClusterClient(callInvoker);
			_bus = bus;
		}

		public void Dispose() {
			if (Disposed) return;
			_channel.Dispose();
			Disposed = true;
		}
	}
}
