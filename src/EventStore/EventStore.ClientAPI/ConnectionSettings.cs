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
    public class ConnectionSettings
    {
        private static readonly Lazy<ConnectionSettings> DefaultSettings = new Lazy<ConnectionSettings>(() => Create(), true);

        /// <summary>
        /// The default <see cref="ConnectionSettings"></see>
        /// </summary>
        public static ConnectionSettings Default
        {
            get
            {
                return DefaultSettings.Value;
            }
        }

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
        /// The maximum number of outstanding items allowed in the queue
        /// </summary>
        public readonly int MaxQueueSize;

        /// <summary>
        /// The maximum number of allowed asyncrhonous operations to be in process
        /// </summary>
        public readonly int MaxConcurrentItems;
        /// <summary>
        /// The maximum number of retry attempts
        /// </summary>
        public readonly int MaxAttempts;
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

        internal ConnectionSettings(ILogger log,
                                    int maxQueueSize,
                                    int maxConcurrentItems,
                                    int maxAttempts,
                                    int maxReconnections,
                                    bool allowForwarding,
                                    TimeSpan reconnectionDelay,
                                    TimeSpan operationTimeout,
                                    TimeSpan operationTimeoutCheckPeriod)
        {
            Log = log;
            MaxQueueSize = maxQueueSize;
            MaxConcurrentItems = maxConcurrentItems;
            MaxAttempts = maxAttempts;
            MaxReconnections = maxReconnections;
            AllowForwarding = allowForwarding;
            ReconnectionDelay = reconnectionDelay;
            OperationTimeout = operationTimeout;
            OperationTimeoutCheckPeriod = operationTimeoutCheckPeriod;
        }
    }

    /// <summary>
    /// Used to build a connection settings (fluent API)
    /// </summary>
    public class ConnectionSettingsBuilder
    {
        private ILogger _log;

        private int _maxQueueSize;
        private int _maxConcurrentItems;
        private int _maxAttempts;
        private int _maxReconnections;

        private bool _allowForwarding;

        private TimeSpan _reconnectionDelay;
        private TimeSpan _operationTimeout;
        private TimeSpan _operationTimeoutCheckPeriod;

        internal ConnectionSettingsBuilder()
        {
            _log = null;

            _maxQueueSize = 5000;
            _maxConcurrentItems = 5000;
            _maxAttempts = 10;
            _maxReconnections = 10;

            _allowForwarding = true;

            _reconnectionDelay = TimeSpan.FromSeconds(3);
            _operationTimeout = TimeSpan.FromSeconds(7);
            _operationTimeoutCheckPeriod = TimeSpan.FromSeconds(1);
        }


        /// <summary>
        /// Configures the connection to utilize a given logger.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger"/> to use.</param>
        /// <returns></returns>
        public ConnectionSettingsBuilder UseLogger(ILogger logger)
        {
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
                                          builder._operationTimeoutCheckPeriod);
        }
    }
}
