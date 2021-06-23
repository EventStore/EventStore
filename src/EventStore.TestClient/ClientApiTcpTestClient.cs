﻿using EventStore.ClientAPI;
using ILogger = Serilog.ILogger;

namespace EventStore.TestClient {
	/// <summary>
	/// A test client that connects using the legacy dotnet TCP client
	/// </summary>
	public class ClientApiTcpTestClient {
		/// <summary>
		/// The options specified when starting the EventStore.TestClient
		/// </summary>
		public ClientOptions Options { get; set; }
		private ILogger _log;

		/// <summary>
		/// Constructs a new <see cref="ClientApiTcpTestClient"/>
		/// </summary>
		/// <param name="options"></param>
		/// <param name="log"></param>
		public ClientApiTcpTestClient(ClientOptions options, ILogger log) {
			Options = options;
			_log = log;
		}

		/// <summary>
		/// Creates a new TCP connection.
		/// </summary>
		/// <returns></returns>
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
