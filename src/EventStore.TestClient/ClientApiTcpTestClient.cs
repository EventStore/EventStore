using EventStore.ClientAPI;
using ILogger = Serilog.ILogger;

namespace EventStore.TestClient {
	public class ClientApiTcpTestClient {
		public ClientOptions Options { get; set; }
		private ILogger _log;

		public ClientApiTcpTestClient(ClientOptions options, ILogger log) {
			Options = options;
			_log = log;
		}

		public IEventStoreConnection CreateConnection() {
			var connectionString = string.IsNullOrWhiteSpace(Options.ConnectionString)
				? $"ConnectTo=tcp://{Options.Host}:{Options.TcpPort};UseSslConnection={Options.UseTls};ValidateServer={Options.TlsValidateServer}"
				: Options.ConnectionString;
			_log.Debug("Creating TCP client with connection string '{connectionString}.", connectionString);

			var connectionSettings = ConnectionSettings.Create()
				.LimitRetriesForOperationTo(0)
				.KeepReconnecting();
			return EventStoreConnection.Create(connectionString, connectionSettings);
		}
	}
}
