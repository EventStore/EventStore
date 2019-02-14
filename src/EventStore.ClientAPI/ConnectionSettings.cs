using System;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI {
	/// <summary>
	/// A <see cref="ConnectionSettings"/> object is an immutable representation of the settings for an
	/// <see cref="IEventStoreConnection"/>. You can build a <see cref="ConnectionSettings"/> object using
	/// a <see cref="ConnectionSettingsBuilder"/>, either via the <see cref="Create"/> method, or via
	/// the constructor of <see cref="ConnectionSettingsBuilder"/>.
	/// </summary>
	public sealed class ConnectionSettings {
		private static readonly Lazy<ConnectionSettings> DefaultSettings =
			new Lazy<ConnectionSettings>(() => Create(), true);

		/// <summary>
		/// The default <see cref="ConnectionSettings"></see>.
		/// </summary>
		public static ConnectionSettings Default {
			get { return DefaultSettings.Value; }
		}

		/// <summary>
		/// Creates a new set of <see cref="ConnectionSettings"/>.
		/// </summary>
		/// <returns>A <see cref="ConnectionSettingsBuilder"/> you can use to build up a <see cref="ConnectionSettings"/></returns>.
		public static ConnectionSettingsBuilder Create() {
			return new ConnectionSettingsBuilder();
		}

		/// <summary>
		/// The <see cref="ILogger"/> that this connection will use.
		/// </summary>
		public readonly ILogger Log;

		/// <summary>
		/// Whether to use excessive logging of <see cref="EventStoreConnection"/> internal logic.
		/// </summary>
		public readonly bool VerboseLogging;

		/// <summary>
		/// The maximum number of outstanding items allowed in the queue.
		/// </summary>
		public readonly int MaxQueueSize;

		/// <summary>
		/// The maximum number of allowed asynchronous operations to be in process.
		/// </summary>
		public readonly int MaxConcurrentItems;

		/// <summary>
		/// The maximum number of retry attempts.
		/// </summary>
		public readonly int MaxRetries;

		/// <summary>
		/// The maximum number of times to allow for reconnection.
		/// </summary>
		public readonly int MaxReconnections;

		/// <summary>
		/// Whether to require EventStore to refuse serving read or write request if it is not master.
		/// </summary>
		public readonly bool RequireMaster;

		/// <summary>
		/// The amount of time to delay before attempting to reconnect.
		/// </summary>
		public readonly TimeSpan ReconnectionDelay;

		/// <summary>
		/// The amount of time a request for an operation is permitted to be queued awaiting transmission to the server.
		/// </summary>
		public readonly TimeSpan QueueTimeout;

		/// <summary>
		/// The amount of time before an operation is considered to have timed out.
		/// </summary>
		public readonly TimeSpan OperationTimeout;

		/// <summary>
		/// The amount of time that timeouts are checked in the system.
		/// </summary>
		public readonly TimeSpan OperationTimeoutCheckPeriod;

		/// <summary>
		/// The <see cref="UserCredentials"/> to use for operations where other <see cref="UserCredentials"/> are not explicitly supplied.
		/// </summary>
		public readonly UserCredentials DefaultUserCredentials;

		/// <summary>
		/// Whether the connection is encrypted using SSL.
		/// </summary>
		public readonly bool UseSslConnection;

		/// <summary>
		/// The host name of the server expected on the SSL certificate.
		/// </summary>
		public readonly string TargetHost;

		/// <summary>
		/// Whether to validate the server SSL certificate.
		/// </summary>
		public readonly bool ValidateServer;

		/// <summary>
		/// Whether to raise an error if no response is received from the server for an operation.
		/// </summary>
		public readonly bool FailOnNoServerResponse;

		/// <summary>
		/// The interval at which to send heartbeat messages.
		/// </summary>
		public readonly TimeSpan HeartbeatInterval;

		/// <summary>
		/// The interval after which an unacknowledged heartbeat will cause the connection to be considered faulted and disconnect.
		/// </summary>
		public readonly TimeSpan HeartbeatTimeout;

		/// <summary>
		/// The DNS name to use for discovering endpoints.
		/// </summary>
		public readonly string ClusterDns;

		/// <summary>
		/// The maximum number of attempts for discovering endpoints.
		/// </summary>
		public readonly int MaxDiscoverAttempts;

		/// <summary>
		/// The well-known endpoint on which cluster managers are running.
		/// </summary>
		public readonly int ExternalGossipPort;

		/// <summary>
		/// Endpoints for seeding gossip if not using DNS.
		/// </summary>
		public readonly GossipSeed[] GossipSeeds;

		/// <summary>
		/// Timeout for cluster gossip.
		/// </summary>
		public readonly TimeSpan GossipTimeout;

		/// <summary>
		/// Whether to randomly choose a node that's alive from the known nodes. 
		/// </summary>
		public readonly NodePreference NodePreference;

		/// <summary>
		/// The interval after which a client will time out during connection.
		/// </summary>
		public readonly TimeSpan ClientConnectionTimeout;

		internal ConnectionSettings(ILogger log,
			bool verboseLogging,
			int maxQueueSize,
			int maxConcurrentItems,
			int maxRetries,
			int maxReconnections,
			bool requireMaster,
			TimeSpan reconnectionDelay,
			TimeSpan queueTimeout,
			TimeSpan operationTimeout,
			TimeSpan operationTimeoutCheckPeriod,
			UserCredentials defaultUserCredentials,
			bool useSslConnection,
			string targetHost,
			bool validateServer,
			bool failOnNoServerResponse,
			TimeSpan heartbeatInterval,
			TimeSpan heartbeatTimeout,
			TimeSpan clientConnectionTimeout,
			string clusterDns,
			GossipSeed[] gossipSeeds,
			int maxDiscoverAttempts,
			int externalGossipPort,
			TimeSpan gossipTimeout,
			NodePreference nodePreference) {
			Ensure.NotNull(log, "log");
			Ensure.Positive(maxQueueSize, "maxQueueSize");
			Ensure.Positive(maxConcurrentItems, "maxConcurrentItems");
			if (maxRetries < -1)
				throw new ArgumentOutOfRangeException("maxRetries",
					string.Format("maxRetries value is out of range: {0}. Allowed range: [-1, infinity].", maxRetries));
			if (maxReconnections < -1)
				throw new ArgumentOutOfRangeException("maxReconnections",
					string.Format("maxReconnections value is out of range: {0}. Allowed range: [-1, infinity].",
						maxRetries));
			if (useSslConnection)
				Ensure.NotNullOrEmpty(targetHost, "targetHost");
			Log = log;
			VerboseLogging = verboseLogging;
			MaxQueueSize = maxQueueSize;
			MaxConcurrentItems = maxConcurrentItems;
			MaxRetries = maxRetries;
			MaxReconnections = maxReconnections;
			RequireMaster = requireMaster;
			ReconnectionDelay = reconnectionDelay;
			QueueTimeout = queueTimeout;
			OperationTimeout = operationTimeout;
			OperationTimeoutCheckPeriod = operationTimeoutCheckPeriod;
			ClientConnectionTimeout = clientConnectionTimeout;
			DefaultUserCredentials = defaultUserCredentials;
			UseSslConnection = useSslConnection;
			TargetHost = targetHost;
			ValidateServer = validateServer;

			FailOnNoServerResponse = failOnNoServerResponse;
			HeartbeatInterval = heartbeatInterval;
			HeartbeatTimeout = heartbeatTimeout;
			ClusterDns = clusterDns;
			GossipSeeds = gossipSeeds;
			MaxDiscoverAttempts = maxDiscoverAttempts;
			ExternalGossipPort = externalGossipPort;
			GossipTimeout = gossipTimeout;
			NodePreference = nodePreference;
		}
	}
}
