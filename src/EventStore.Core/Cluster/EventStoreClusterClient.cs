using System;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using Grpc.Net.Client;
using Serilog.Extensions.Logging;

namespace EventStore.Core.Cluster {
	
	public partial class EventStoreClusterClient : IDisposable {
		private readonly EventStore.Cluster.Gossip.GossipClient _gossipClient;
		private readonly EventStore.Cluster.Elections.ElectionsClient _electionsClient;
		
		private readonly GrpcChannel _channel;
		private readonly IPublisher _bus;
		internal bool Disposed { get; private set; }

		public EventStoreClusterClient(Uri address, IPublisher bus,
			Func<HttpMessageHandler> httpMessageHandlerFactory = null) {

			HttpMessageHandler httpMessageHandler = httpMessageHandlerFactory?.Invoke() ?? new HttpClientHandler {
				ServerCertificateCustomValidationCallback = (message, certificate, chain, errors) => {
					if (errors != SslPolicyErrors.None) {
						Log.Error("Certificate validation failed for certificate: {certificate} due to reason {reason}.", certificate, errors);
					}
					return errors == SslPolicyErrors.None;
				}
			};

			_channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions {
				HttpClient = new HttpClient(httpMessageHandler) {
					Timeout = Timeout.InfiniteTimeSpan,
					DefaultRequestVersion = new Version(2, 0),
				},
				LoggerFactory = new SerilogLoggerFactory()
			});
			var callInvoker = _channel.CreateCallInvoker();
			_gossipClient = new EventStore.Cluster.Gossip.GossipClient(callInvoker);
			_electionsClient = new EventStore.Cluster.Elections.ElectionsClient(callInvoker);
			_bus = bus;
		}

		public void Dispose() {
			if (Disposed) return;
			_channel.Dispose();
			Disposed = true;
		}
	}
}
