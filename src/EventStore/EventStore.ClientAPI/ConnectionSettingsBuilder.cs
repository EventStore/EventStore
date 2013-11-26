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

using System;
using EventStore.ClientAPI.Common.Log;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI
{
    /// <summary>
    /// Used to build a connection settings (fluent API)
    /// </summary>
    public class ConnectionSettingsBuilder
    {
        private ILogger _log = new NoopLogger();
        private bool _verboseLogging;

        private int _maxQueueSize = Consts.DefaultMaxQueueSize;
        private int _maxConcurrentItems = Consts.DefaultMaxConcurrentItems;
        private int _maxRetries = Consts.DefaultMaxOperationRetries;
        private int _maxReconnections = Consts.DefaultMaxReconnections;

        private bool _requireMaster = Consts.DefaultRequireMaster;

        private TimeSpan _reconnectionDelay = Consts.DefaultReconnectionDelay;
        private TimeSpan _operationTimeout = Consts.DefaultOperationTimeout;
        private TimeSpan _operationTimeoutCheckPeriod = Consts.DefaultOperationTimeoutCheckPeriod;

        private UserCredentials _defaultUserCredentials;
        private bool _useSslConnection;
        private string _targetHost;
        private bool _validateServer;

        private bool _failOnNoServerResponse;
        private TimeSpan _heartbeatInterval = TimeSpan.FromMilliseconds(750);
        private TimeSpan _heartbeatTimeout = TimeSpan.FromMilliseconds(1500);
        private TimeSpan _clientConnectionTimeout = TimeSpan.FromMilliseconds(1000);

        internal ConnectionSettingsBuilder()
        {
        }

        /// <summary>
        /// Configures the connection to output log messages to the given <see cref="ILogger" />.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger"/> to use.</param>
        /// <returns></returns>
        public ConnectionSettingsBuilder UseCustomLogger(ILogger logger)
        {
            Ensure.NotNull(logger, "logger");
            _log = logger;
            return this;
        }

        /// <summary>
        /// Configures the connection to output log messages to the console.
        /// </summary>
        public ConnectionSettingsBuilder UseConsoleLogger()
        {
            _log = new ConsoleLogger();
            return this;
        }

        /// <summary>
        /// Configures the connection to output log messages to the listeners
        /// configured on <see cref="System.Diagnostics.Debug" />.
        /// </summary>
        public ConnectionSettingsBuilder UseDebugLogger()
        {
            _log = new DebugLogger();
            return this;
        }

        /// <summary>
        /// Turns on verbose <see cref="EventStoreConnection"/> internal logic logging.
        /// </summary>
        /// <returns></returns>
        public ConnectionSettingsBuilder EnableVerboseLogging()
        {
            _verboseLogging = true;
            return this;
        }

        /// <summary>
        /// Sets the limit for number of outstanding operations
        /// </summary>
        /// <param name="limit">The new limit of outstanding operations</param>
        /// <returns></returns>
        public ConnectionSettingsBuilder LimitOperationsQueueTo(int limit)
        {
            Ensure.Positive(limit, "limit");

            _maxQueueSize = limit;
            return this;
        }

        /// <summary>
        /// Limits the number of concurrent operations that this connection can have
        /// </summary>
        /// <param name="limit"></param>
        /// <returns></returns>
        public ConnectionSettingsBuilder LimitConcurrentOperationsTo(int limit)
        {
            Ensure.Positive(limit, "limit");

            _maxConcurrentItems = limit;
            return this;
        }

        /// <summary>
        /// Limits the number of operation attempts
        /// </summary>
        /// <param name="limit"></param>
        /// <returns></returns>
        public ConnectionSettingsBuilder LimitAttemptsForOperationTo(int limit)
        {
            Ensure.Positive(limit, "limit");

            _maxRetries = limit - 1;
            return this;
        }

        /// <summary>
        /// Limits the number of operation retries
        /// </summary>
        /// <param name="limit"></param>
        /// <returns></returns>
        public ConnectionSettingsBuilder LimitRetriesForOperationTo(int limit)
        {
            Ensure.Nonnegative(limit, "limit");

            _maxRetries = limit;
            return this;
        }

        /// <summary>
        /// Allows infinite operation retry attempts
        /// </summary>
        /// <returns></returns>
        public ConnectionSettingsBuilder KeepRetrying()
        {
            _maxRetries = -1;
            return this;
        }

        /// <summary>
        /// Limits the number of reconnections this connection can try to make
        /// </summary>
        /// <param name="limit"></param>
        /// <returns></returns>
        public ConnectionSettingsBuilder LimitReconnectionsTo(int limit)
        {
            Ensure.Nonnegative(limit, "limit");

            _maxReconnections = limit;
            return this;
        }

        /// <summary>
        /// Allows infinite reconnection attempts
        /// </summary>
        /// <returns></returns>
        public ConnectionSettingsBuilder KeepReconnecting()
        {
            _maxReconnections = -1;
            return this;
        }

        /// <summary>
        /// Requires all write and read requests to be served only by master (cluster version only) 
        /// </summary>
        /// <returns></returns>
        public ConnectionSettingsBuilder PerformOnMasterOnly()
        {
            _requireMaster = true;
            return this;
        }

        /// <summary>
        /// Allow for writes to be forwarded and read requests served locally if node is not master (cluster version only) 
        /// </summary>
        /// <returns></returns>
        public ConnectionSettingsBuilder PerformOnAnyNode()
        {
            _requireMaster = false;
            return this;
        }

        /// <summary>
        /// Sets the delay between reconnection attempts
        /// </summary>
        /// <param name="reconnectionDelay"></param>
        /// <returns></returns>
        public ConnectionSettingsBuilder SetReconnectionDelayTo(TimeSpan reconnectionDelay)
        {
            _reconnectionDelay = reconnectionDelay;
            return this;
        }

        /// <summary>
        /// Sets the operation timeout duration
        /// </summary>
        /// <param name="operationTimeout"></param>
        /// <returns></returns>
        public ConnectionSettingsBuilder SetOperationTimeoutTo(TimeSpan operationTimeout)
        {
            _operationTimeout = operationTimeout;
            return this;
        }

        /// <summary>
        /// Sets how often timeouts should be checked for.
        /// </summary>
        /// <param name="timeoutCheckPeriod"></param>
        /// <returns></returns>
        public ConnectionSettingsBuilder SetTimeoutCheckPeriodTo(TimeSpan timeoutCheckPeriod)
        {
            _operationTimeoutCheckPeriod = timeoutCheckPeriod;
            return this;
        }

        /// <summary>
        /// Sets the default <see cref="UserCredentials"/> to be used for this connection.
        /// If user credentials are not given for an operation, these credentials will be used.
        /// </summary>
        /// <param name="userCredentials"></param>
        /// <returns></returns>
        public ConnectionSettingsBuilder SetDefaultUserCredentials(UserCredentials userCredentials)
        {
            _defaultUserCredentials = userCredentials;
            return this;
        }

        /// <summary>
        /// Uses a SSL connection over TCP. This should generally be used with authentication.
        /// </summary>
        /// <param name="targetHost">HostName of server certificate.</param>
        /// <param name="validateServer">Whether to accept connection from server with not trusted certificate.</param>
        /// <returns></returns>
        public ConnectionSettingsBuilder UseSslConnection(string targetHost, bool validateServer)
        {
            Ensure.NotNullOrEmpty(targetHost, "targetHost");
            _useSslConnection = true;
            _targetHost = targetHost;
            _validateServer = validateServer;
            return this;
        }

        /// <summary>
        /// Marks that no response from server should cause an error on the request
        /// </summary>
        /// <returns></returns>
        public ConnectionSettingsBuilder FailOnNoServerResponse()
        {
            _failOnNoServerResponse = true;
            return this;
        }

        /// <summary>
        /// Sets how often heartbeats should be expected on the connection (lower values detect broken sockets faster)
        /// </summary>
        /// <param name="interval"></param>
        /// <returns></returns>
        public ConnectionSettingsBuilder SetHeartbeatInterval(TimeSpan interval)
        {
            _heartbeatInterval = interval;
            return this;
        }

        /// <summary>
        /// Sets how long to wait without heartbeats before determining a connection to be dead (must be longer than heartbeat interval)
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public ConnectionSettingsBuilder SetHeartbeatTimeout(TimeSpan timeout)
        {
            _heartbeatTimeout = timeout;
            return this;
        }

        /// <summary>
        /// Sets the timeout for attempting to connect to a server before aborting and attempting a reconnect.
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public ConnectionSettingsBuilder WithConnectionTimeoutOf(TimeSpan timeout)
        {
            _clientConnectionTimeout = timeout;
            return this;
        }

        /// <summary>
        /// Convert the mutable <see cref="ConnectionSettingsBuilder"/> object to an immutable
        /// <see cref="ConnectionSettings"/> object.
        /// </summary>
        /// <param name="builder">The <see cref="ConnectionSettingsBuilder"/> to convert.</param>
        /// <returns>An immutable <see cref="ConnectionSettings"/> object with the values specified by the builder.</returns>
        public static implicit operator ConnectionSettings(ConnectionSettingsBuilder builder)
        {
            return new ConnectionSettings(builder._log,
                                          builder._verboseLogging,
                                          builder._maxQueueSize,
                                          builder._maxConcurrentItems,
                                          builder._maxRetries,
                                          builder._maxReconnections,
                                          builder._requireMaster,
                                          builder._reconnectionDelay,
                                          builder._operationTimeout,
                                          builder._operationTimeoutCheckPeriod,
                                          builder._defaultUserCredentials,
                                          builder._useSslConnection,
                                          builder._targetHost,
                                          builder._validateServer,
                                          builder._failOnNoServerResponse,
                                          builder._heartbeatInterval,
                                          builder._heartbeatTimeout,
                                          builder._clientConnectionTimeout);
        }
    }
}