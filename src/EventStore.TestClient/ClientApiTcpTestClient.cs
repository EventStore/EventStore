using EventStore.ClientAPI;
using ILogger = Serilog.ILogger;

namespace EventStore.TestClient {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
	public class ClientApiTcpTestClient {
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public ClientOptions Options { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
		private ILogger _log;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public ClientApiTcpTestClient(ClientOptions options, ILogger log) {
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
			Options = options;
			_log = log;
		}

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public IEventStoreConnection CreateConnection() {
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
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
