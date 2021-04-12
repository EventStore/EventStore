using EventStore.Client;
using Connection = EventStore.Transport.Tcp.TcpTypedConnection<byte[]>;
using ILogger = Serilog.ILogger;

namespace EventStore.TestClient {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
	public class GrpcTestClient {
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
		private ClientOptions _options;
		private ILogger _log;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public GrpcTestClient(ClientOptions options, ILogger log) {
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
			_options = options;
			_log = log;
		}

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public EventStoreClient CreateGrpcClient() {
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
			var connectionString = string.IsNullOrWhiteSpace(_options.ConnectionString)
				? $"esdb://{_options.Host}:{_options.HttpPort}?tls={_options.UseTls}&tlsVerifyCert={_options.TlsValidateServer}"
				: _options.ConnectionString;
			_log.Debug("Creating gRPC client with connection string '{connectionString}'.", connectionString);
			var settings = EventStoreClientSettings.Create(connectionString);
			return new EventStoreClient(settings);
		}
	}
}
