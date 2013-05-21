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

        private bool _allowForwarding = Consts.DefaultAllowForwarding;

        private TimeSpan _reconnectionDelay = Consts.DefaultReconnectionDelay;
        private TimeSpan _operationTimeout = Consts.DefaultOperationTimeout;
        private TimeSpan _operationTimeoutCheckPeriod = Consts.DefaultOperationTimeoutCheckPeriod;

        private UserCredentials _defaultUserCredentials;
        private bool _useSslConnection;
        private string _targetHost;
        private bool _validateServer;

        private Action<IEventStoreConnection, Exception> _errorOccurred;
        private Action<IEventStoreConnection, string> _closed;
        private Action<IEventStoreConnection> _connected;
        private Action<IEventStoreConnection> _disconnected;
        private Action<IEventStoreConnection> _reconnecting;
        private Action<IEventStoreConnection, string> _authenticationFailed;

        private bool _failOnNoServerResponse;
        private TimeSpan _heartbeatInterval = TimeSpan.FromMilliseconds(750);
        private TimeSpan _heartbeatTimeout = TimeSpan.FromMilliseconds(1500);

        internal ConnectionSettingsBuilder()
        {
        }

        /// <summary>
        /// Configures the connection not to output log messages. This is the default.
        /// </summary>
        public ConnectionSettingsBuilder DoNotLog()
        {
            _log = new NoopLogger();
            return this;
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
        /// Turns on excessive <see cref="EventStoreConnection"/> internal logic logging.
        /// </summary>
        /// <returns></returns>
        public ConnectionSettingsBuilder EnableVerboseLogging()
        {
            _verboseLogging = true;
            return this;
        }

        /// <summary>
        /// Turns off excessive <see cref="EventStoreConnection"/> internal logic logging.
        /// </summary>
        /// <returns></returns>
        public ConnectionSettingsBuilder DisableVerboseLogging()
        {
            _verboseLogging = false;
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
        /// Enables the forwarding of operations in the Event Store (cluster version only) 
        /// </summary>
        /// <returns></returns>
        public ConnectionSettingsBuilder EnableOperationsForwarding()
        {
            _allowForwarding = true;
            return this;
        }

        /// <summary>
        /// Disables the forwarding operations in the Event Store (cluster version only)
        /// </summary>
        /// <returns></returns>
        public ConnectionSettingsBuilder DisableOperationsForwarding()
        {
            _allowForwarding = false;
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

        public ConnectionSettingsBuilder SetDefaultUserCredentials(UserCredentials userCredentials)
        {
            _defaultUserCredentials = userCredentials;
            return this;
        }

        /// <summary>
        /// Tells to use SSL TCP connection.
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
        /// Tells to use normal TCP connection.
        /// </summary>
        /// <returns></returns>
        public ConnectionSettingsBuilder UseNormalConnection()
        {
            _useSslConnection = false;
            _targetHost = null;
            _validateServer = true;
            return this;
        }

        /// <summary>
        /// Sets handler of internal connection errors.
        /// </summary>
        /// <param name="handler"></param>
        /// <returns></returns>
        public ConnectionSettingsBuilder OnErrorOccurred(Action<IEventStoreConnection, Exception> handler)
        {
            _errorOccurred = handler;
            return this;
        }

        /// <summary>
        /// Sets handler of <see cref="EventStoreConnection"/> closing.
        /// </summary>
        /// <param name="handler"></param>
        /// <returns></returns>
        public ConnectionSettingsBuilder OnClosed(Action<IEventStoreConnection, string> handler)
        {
            _closed = handler;
            return this;
        }

        /// <summary>
        /// Sets handler called when connection is established.
        /// </summary>
        /// <param name="handler"></param>
        /// <returns></returns>
        public ConnectionSettingsBuilder OnConnected(Action<IEventStoreConnection> handler)
        {
            _connected = handler;
            return this;
        }

        /// <summary>
        /// Sets handler called when connection is dropped.
        /// </summary>
        /// <param name="handler"></param>
        /// <returns></returns>
        public ConnectionSettingsBuilder OnDisconnected(Action<IEventStoreConnection> handler)
        {
            _disconnected = handler;
            return this;
        }

        /// <summary>
        /// Sets handler called when reconnection attempt is made.
        /// </summary>
        /// <param name="handler"></param>
        /// <returns></returns>
        public ConnectionSettingsBuilder OnReconnecting(Action<IEventStoreConnection> handler)
        {
            _reconnecting = handler;
            return this;
        }

        /// <summary>
        /// Sets handler called when authentication failure occurs.
        /// </summary>
        /// <param name="handler"></param>
        /// <returns></returns>
        public ConnectionSettingsBuilder OnAuthenticationFailed(Action<IEventStoreConnection, string> handler)
        {
            _authenticationFailed = handler;
            return this;
        }

        public ConnectionSettingsBuilder FailOnNoServerResponse()
        {
            _failOnNoServerResponse = true;
            return this;
        }

        public ConnectionSettingsBuilder DoNotFailOnNoServerResponse()
        {
            _failOnNoServerResponse = false;
            return this;
        }

        public ConnectionSettingsBuilder SetHeartbeatInterval(TimeSpan interval)
        {
            _heartbeatInterval = interval;
            return this;
        }

        public ConnectionSettingsBuilder SetHeartbeatTimeout(TimeSpan timeout)
        {
            _heartbeatTimeout = timeout;
            return this;
        }

        public static implicit operator ConnectionSettings(ConnectionSettingsBuilder builder)
        {
            return new ConnectionSettings(builder._log,
                                          builder._verboseLogging,
                                          builder._maxQueueSize,
                                          builder._maxConcurrentItems,
                                          builder._maxRetries,
                                          builder._maxReconnections,
                                          builder._allowForwarding,
                                          builder._reconnectionDelay,
                                          builder._operationTimeout,
                                          builder._operationTimeoutCheckPeriod,
                                          builder._defaultUserCredentials,
                                          builder._useSslConnection,
                                          builder._targetHost,
                                          builder._validateServer,
                                          builder._errorOccurred,
                                          builder._closed,
                                          builder._connected,
                                          builder._disconnected,
                                          builder._reconnecting,
                                          builder._authenticationFailed,
                                          builder._failOnNoServerResponse,
                                          builder._heartbeatInterval,
                                          builder._heartbeatTimeout);
        }
    }
}