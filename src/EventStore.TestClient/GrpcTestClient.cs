using EventStore.Client;
using Connection = EventStore.Transport.Tcp.TcpTypedConnection<byte[]>;
using ILogger = Serilog.ILogger;

namespace EventStore.TestClient {
	public class GrpcTestClient {
		private ClientOptions _options;
		private ILogger _log;

		public GrpcTestClient(ClientOptions options, ILogger log) {
			_options = options;
			_log = log;
		}

		public EventStoreClient CreateGrpcClient() {
			var connectionString = string.IsNullOrWhiteSpace(_options.ConnectionString)
				? $"esdb://{_options.Host}:{_options.HttpPort}?tls={_options.UseTls}&tlsVerifyCert={_options.TlsValidateServer}"
				: _options.ConnectionString;
			_log.Debug("Creating gRPC client with connection string '{connectionString}'.", connectionString);
			var settings = EventStoreClientSettings.Create(connectionString);
			return new EventStoreClient(settings);
		}
	}
}
