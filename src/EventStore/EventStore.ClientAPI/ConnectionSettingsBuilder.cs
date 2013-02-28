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

namespace EventStore.ClientAPI
{
    /// <summary>
    /// Used to build a connection settings (fluent API)
    /// </summary>
    public class ConnectionSettingsBuilder
    {
        private ILogger _log = new NoopLogger();

        private int _maxQueueSize = Consts.DefaultMaxQueueSize;
        private int _maxConcurrentItems = Consts.DefaultMaxConcurrentItems;
        private int _maxAttempts = Consts.DefaultMaxOperationAttempts;
        private int _maxReconnections = Consts.DefaultMaxReconnections;

        private bool _allowForwarding = Consts.DefaultAllowForwarding;

        private TimeSpan _reconnectionDelay = Consts.DefaultReconnectionDelay;
        private TimeSpan _operationTimeout = Consts.DefaultOperationTimeout;
        private TimeSpan _operationTimeoutCheckPeriod = Consts.DefaultOperationTimeoutCheckPeriod;

        private Action<EventStoreConnection, Exception> _errorOccurred;
        private Action<EventStoreConnection, string> _closed;
        private Action<EventStoreConnection> _connected;
        private Action<EventStoreConnection> _disconnected;
        private Action<EventStoreConnection> _reconnecting;

        internal ConnectionSettingsBuilder()
        {
        }

        /// <summary>
        /// Configures the connection to utilize a given logger.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger"/> to use.</param>
        /// <returns></returns>
        public ConnectionSettingsBuilder UseLogger(ILogger logger)
        {
            Ensure.NotNull(logger, "logger");
            _log = logger;
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
        /// Limits the number of retry attempts for a given operation
        /// </summary>
        /// <param name="limit"></param>
        /// <returns></returns>
        public ConnectionSettingsBuilder LimitAttemptsForOperationTo(int limit)
        {
            Ensure.Positive(limit, "limit");

            _maxAttempts = limit;
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

        /// <summary>
        /// Sets handler of internal connection errors.
        /// </summary>
        /// <param name="handler"></param>
        /// <returns></returns>
        public ConnectionSettingsBuilder OnErrorOccurred(Action<EventStoreConnection, Exception> handler)
        {
            _errorOccurred = handler;
            return this;
        }

        /// <summary>
        /// Sets handler of <see cref="EventStoreConnection"/> closing.
        /// </summary>
        /// <param name="handler"></param>
        /// <returns></returns>
        public ConnectionSettingsBuilder OnClosed(Action<EventStoreConnection, string> handler)
        {
            _closed = handler;
            return this;
        }

        /// <summary>
        /// Sets handler called when connection is established.
        /// </summary>
        /// <param name="handler"></param>
        /// <returns></returns>
        public ConnectionSettingsBuilder OnConnected(Action<EventStoreConnection> handler)
        {
            _connected = handler;
            return this;
        }

        /// <summary>
        /// Sets handler called when connection is dropped.
        /// </summary>
        /// <param name="handler"></param>
        /// <returns></returns>
        public ConnectionSettingsBuilder OnDisconnected(Action<EventStoreConnection> handler)
        {
            _disconnected = handler;
            return this;
        }

        /// <summary>
        /// Sets handler called when reconnection attempt is made.
        /// </summary>
        /// <param name="handler"></param>
        /// <returns></returns>
        public ConnectionSettingsBuilder OnReconnecting(Action<EventStoreConnection> handler)
        {
            _reconnecting = handler;
            return this;
        }

        public static implicit operator ConnectionSettings(ConnectionSettingsBuilder builder)
        {
            return new ConnectionSettings(builder._log,
                                          builder._maxQueueSize,
                                          builder._maxConcurrentItems,
                                          builder._maxAttempts,
                                          builder._maxReconnections,
                                          builder._allowForwarding,
                                          builder._reconnectionDelay,
                                          builder._operationTimeout,
                                          builder._operationTimeoutCheckPeriod,
                                          builder._errorOccurred,
                                          builder._closed,
                                          builder._connected,
                                          builder._disconnected,
                                          builder._reconnecting);
        }
    }
}