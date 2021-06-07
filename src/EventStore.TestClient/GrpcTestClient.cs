using EventStore.Client;
using Connection = EventStore.Transport.Tcp.TcpTypedConnection<byte[]>;
using ILogger = Serilog.ILogger;

namespace EventStore.TestClient {
	/// <summary>
	/// A test client that connects using the dotnet gRPC client
	/// </summary>
	public class GrpcTestClient {
		private ClientOptions _options;
		private ILogger _log;

		/// <summary>
		/// Constructs a new <see cref="GrpcTestClient"/>
		/// </summary>
		/// <param name="options"></param>
		/// <param name="log"></param>
		public GrpcTestClient(ClientOptions options, ILogger log) {
			_options = options;
			_log = log;
		}

		/// <summary>
		/// Creates a new gRPC client
		/// </summary>
		/// <returns></returns>
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
