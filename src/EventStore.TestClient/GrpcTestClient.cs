using EventStore.Client;
using Connection = EventStore.Transport.Tcp.TcpTypedConnection<byte[]>;
using ILogger = Serilog.ILogger;

namespace EventStore.TestClient {
	/// <summary>
	/// The dotnet gRPC client used by the TestClient commands that have the 'grpc' suffix
	/// </summary>
	public class GrpcTestClient {
		private ClientOptions _options;
		private ILogger _log;

		/// <summary>
		/// Creates a dotnet gRPC Test Client for a command
		/// </summary>
		/// <param name="options">The ClientOptions for the TestClient.</param>
		/// <param name="log"></param>
		public GrpcTestClient(ClientOptions options, ILogger log) {
			_options = options;
			_log = log;
		}

		/// <summary>
		/// Creates a gRPC client for the command to use for its tests
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
