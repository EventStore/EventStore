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

		public EventStoreClusterClient(Uri address, IPublisher bus, Func<X509Certificate, X509Chain, SslPolicyErrors, ValueTuple<bool, string>> serverCertValidator, Func<X509Certificate> clientCertificateSelector) {
			HttpMessageHandler httpMessageHandler = null;
			if (address.Scheme == Uri.UriSchemeHttps){
				var socketsHttpHandler = new SocketsHttpHandler {
					SslOptions = {
						RemoteCertificateValidationCallback = (sender, certificate, chain, errors) => {
							var (isValid, error) = serverCertValidator(certificate, chain, errors);
							if (!isValid && error != null) {
								Log.Error("Server certificate validation error: {e}", error);
							}

							return isValid;
						},
						LocalCertificateSelectionCallback = delegate {
							return clientCertificateSelector();
						}
					}
				};

				httpMessageHandler = socketsHttpHandler;
			} else if (address.Scheme == Uri.UriSchemeHttp) {
				AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
				httpMessageHandler = new SocketsHttpHandler();
			}

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
