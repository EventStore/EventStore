using System;
using System.Net.Http;
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

		public EventStoreClusterClient(Uri address, IPublisher bus, string tlsTargetHost = null,
			Func<HttpMessageHandler> httpMessageHandlerFactory = null) {
			if(tlsTargetHost == null && address.Scheme == Uri.UriSchemeHttps)
				throw new Exception("Address scheme is https but TLS target host not specified.");

			HttpMessageHandler httpMessageHandler = httpMessageHandlerFactory?.Invoke() ?? new HttpClientHandler {
				ServerCertificateCustomValidationCallback = (message, certificate, chain, errors) => {
					bool isValid = true;
					chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
					if (!chain.Build(certificate)) {
						Log.Error("Certificate chain validation failed for certificate: {certificate}.", certificate);
						isValid = false;
					}

					if (certificate.GetNameInfo(X509NameType.SimpleName, false) != tlsTargetHost) {
						Log.Error("Certificate Common Name does not match TLS target host {tlsTargetHost} for certificate: {certificate}.", tlsTargetHost, certificate);
						isValid = false;
					}

					return isValid;
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
