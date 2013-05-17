// Copyright (c) 2012, Event Store LLP
// All rights reserved.
//  
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
//  
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//  

using System;
using EventStore.ClientAPI.Common.Utils;

namespace EventStore.ClientAPI
{
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
        public readonly ILogger Log;
        /// <summary>
        /// Whether or not do excessive logging of <see cref="EventStoreConnection"/> internal logic.
        /// </summary>
        public readonly bool VerboseLogging;
        /// <summary>
        /// The maximum number of outstanding items allowed in the queue
        /// </summary>
        public readonly int MaxQueueSize;
        /// <summary>
        /// The maximum number of allowed asynchronous operations to be in process
        /// </summary>
        public readonly int MaxConcurrentItems;
        /// <summary>
        /// The maximum number of retry attempts
        /// </summary>
        public readonly int MaxRetries;
        /// <summary>
        /// The maximum number of times to allow for reconnection
        /// </summary>
        public readonly int MaxReconnections;
        /// <summary>
        /// Whether or not to allow the event store to forward a message if it is unable to process it (cluster version only)
        /// </summary>
        public readonly bool AllowForwarding;
        /// <summary>
        /// The amount of time to delay before attempting to reconnect
        /// </summary>
        public readonly TimeSpan ReconnectionDelay;
        /// <summary>
        /// The amount of time before an operation is considered to have timed out
        /// </summary>
        public readonly TimeSpan OperationTimeout;
        /// <summary>
        /// The amount of time that timeouts are checked in the system.
        /// </summary>
        public readonly TimeSpan OperationTimeoutCheckPeriod;

        public readonly bool UseSslConnection;
        public readonly string TargetHost;
        public readonly bool ValidateServer;

        /// <summary>
        /// Raised whenever the internal error occurs
        /// </summary>
        public Action<IEventStoreConnection, Exception> ErrorOccurred;
        /// <summary>
        /// Raised whenever the connection is closed
        /// </summary>
        public Action<IEventStoreConnection, string> Closed;
        /// <summary>
        /// Raised whenever the internal connection is connected to the event store
        /// </summary>
        public Action<IEventStoreConnection> Connected;
        /// <summary>
        /// Raised whenever the internal connection is disconnected from the event store
        /// </summary>
        public Action<IEventStoreConnection> Disconnected;
        /// <summary>
        /// Raised whenever the internal connection is reconnecting to the event store
        /// </summary>
        public Action<IEventStoreConnection> Reconnecting;

        public readonly bool FailOnNoServerResponse;
        public readonly TimeSpan HeartbeatInterval;
        public readonly TimeSpan HeartbeatTimeout;

        internal ConnectionSettings(ILogger log,
                                    bool verboseLogging,
                                    int maxQueueSize,
                                    int maxConcurrentItems,
                                    int maxRetries,
                                    int maxReconnections,
                                    bool allowForwarding,
                                    TimeSpan reconnectionDelay,
                                    TimeSpan operationTimeout,
                                    TimeSpan operationTimeoutCheckPeriod,
                                    bool useSslConnection,
                                    string targetHost,
                                    bool validateServer,
                                    Action<IEventStoreConnection, Exception> errorOccurred,
                                    Action<IEventStoreConnection, string> closed,
                                    Action<IEventStoreConnection> connected,
                                    Action<IEventStoreConnection> disconnected,
                                    Action<IEventStoreConnection> reconnecting,
                                    bool failOnNoServerResponse,
                                    TimeSpan heartbeatInterval,
                                    TimeSpan heartbeatTimeout)
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
            AllowForwarding = allowForwarding;
            ReconnectionDelay = reconnectionDelay;
            OperationTimeout = operationTimeout;
            OperationTimeoutCheckPeriod = operationTimeoutCheckPeriod;

            UseSslConnection = useSslConnection;
            TargetHost = targetHost;
            ValidateServer = validateServer;

            ErrorOccurred = errorOccurred;
            Closed = closed;
            Connected = connected;
            Disconnected = disconnected;
            Reconnecting = reconnecting;

            FailOnNoServerResponse = failOnNoServerResponse;
            HeartbeatInterval = heartbeatInterval;
            HeartbeatTimeout = heartbeatTimeout;
        }
    }
}
