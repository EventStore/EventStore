using System;
using System.Collections.Generic;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI
{
    /// <summary>
    /// A <see cref="ConnectionSettings"/> object is an immutable representation of the settings for an
    /// <see cref="IEventStoreConnection"/>. A <see cref="ConnectionSettings"/> object can be built using
    /// a <see cref="ConnectionSettingsBuilder"/>, either via the <see cref="Create"/> method, or via
    /// the constructor of <see cref="ConnectionSettingsBuilder"/>.
    /// </summary>
    public sealed class ConnectionSettings
    {
        private static readonly Lazy<ConnectionSettings> DefaultSettings = new Lazy<ConnectionSettings>(() => Create(), true);

        /// <summary>
        /// The default <see cref="ConnectionSettings"></see>
        /// </summary>
        public static ConnectionSettings Default { get { return DefaultSettings.Value; } }

        /// <summary>
        /// Creates a new set of <see cref="ConnectionSettings"/>
        /// </summary>
        /// <returns>A <see cref="ConnectionSettingsBuilder"/> that can be used to build up a <see cref="ConnectionSettings"/></returns>
        public static ConnectionSettingsBuilder Create()
        {
            return new ConnectionSettingsBuilder();
        }

        /// <summary>
        /// The <see cref="ILogger"/> that this connection will use
        /// </summary>
        public ILogger Log { get; private set; }

        /// <summary>
        /// Whether or not do excessive logging of <see cref="EventStoreConnection"/> internal logic.
        /// </summary>
        public bool VerboseLogging { get; private set; }

        /// <summary>
        /// The maximum number of outstanding items allowed in the queue
        /// </summary>
        public int MaxQueueSize { get; private set; }

        /// <summary>
        /// The maximum number of allowed asynchronous operations to be in process
        /// </summary>
        public int MaxConcurrentItems { get; private set; }

        /// <summary>
        /// The maximum number of retry attempts
        /// </summary>
        public int MaxRetries { get; private set; }

        /// <summary>
        /// The maximum number of times to allow for reconnection
        /// </summary>
        public int MaxReconnections { get; private set; }

        /// <summary>
        /// Whether or not to require EventStore to refuse serving read or write request if it is not master
        /// </summary>
        public bool RequireMaster { get; private set; }
        
        /// <summary>
        /// The amount of time to delay before attempting to reconnect
        /// </summary>
        public TimeSpan ReconnectionDelay { get; private set; }

        /// <summary>
        /// The amount of time before an operation is considered to have timed out
        /// </summary>
        public TimeSpan OperationTimeout { get; private set; }

        /// <summary>
        /// The amount of time that timeouts are checked in the system.
        /// </summary>
        public TimeSpan OperationTimeoutCheckPeriod { get; private set; }

        /// <summary>
        /// The <see cref="UserCredentials"/> to use for operations where other <see cref="UserCredentials"/> are not explicitly supplied.
        /// </summary>
        public UserCredentials DefaultUserCredentials { get; private set; }

        /// <summary>
        /// Whether or not the connection is encrypted using SSL.
        /// </summary>
        public bool UseSslConnection { get; private set; }

        /// <summary>
        /// The host name of the server expected on the SSL certificate.
        /// </summary>
        public string TargetHost { get; private set; }

        /// <summary>
        /// Whether or not to validate the server SSL certificate.
        /// </summary>
        public bool ValidateServer { get; private set; }

        /// <summary>
        /// Whether or not to raise an error if no response is received from the server for an operation.
        /// </summary>
        public bool FailOnNoServerResponse { get; private set; }

        /// <summary>
        /// The interval at which to send heartbeat messages.
        /// </summary>
        public TimeSpan HeartbeatInterval { get; private set; }

        /// <summary>
        /// The interval after which an unacknowledged heartbeat will cause the connection to be considered faulted and disconnect.
        /// </summary>
        public TimeSpan HeartbeatTimeout { get; private set; }


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
                                    TimeSpan operationTimeout,
                                    TimeSpan operationTimeoutCheckPeriod,
                                    UserCredentials defaultUserCredentials,
                                    bool useSslConnection,
                                    string targetHost,
                                    bool validateServer,
                                    bool failOnNoServerResponse,
                                    TimeSpan heartbeatInterval,
                                    TimeSpan heartbeatTimeout,
                                    TimeSpan clientConnectionTimeout)
        {
            Ensure.NotNull(log, "log");
            Ensure.Positive(maxQueueSize, "maxQueueSize");
            Ensure.Positive(maxConcurrentItems, "maxConcurrentItems");
            if (maxRetries < -1)
                throw new ArgumentOutOfRangeException("maxRetries", string.Format("maxRetries value is out of range: {0}. Allowed range: [-1, infinity].", maxRetries));
            if (maxReconnections < -1)
                throw new ArgumentOutOfRangeException("maxReconnections", string.Format("maxReconnections value is out of range: {0}. Allowed range: [-1, infinity].", maxRetries));
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
        }
    }
}
