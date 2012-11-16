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

        public static ConnectionSettings Default
        {
            get
            {
                return DefaultSettings.Value;
            }
        }

        public static ConnectionSettingsBuilder Create()
        {
            return new ConnectionSettingsBuilder();
        }

        public readonly ILogger Log;
        public readonly int MaxQueueSize;
        public readonly int MaxConcurrentItems;
        public readonly int MaxAttempts;
        public readonly int MaxReconnections;
        public readonly bool AllowForwarding;
        public readonly TimeSpan ReconnectionDelay;
        public readonly TimeSpan OperationTimeout;
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

        public ConnectionSettingsBuilder UseLogger(ILogger logger)
        {
            _log = logger;
            return this;
        }

        internal ConnectionSettingsBuilder LimitOperationsQueueTo(int limit)
        {
            Ensure.Positive(limit, "limit");

            _maxQueueSize = limit;
            return this;
        }

        public ConnectionSettingsBuilder LimitConcurrentOperationsTo(int limit)
        {
            Ensure.Positive(limit, "limit");

            _maxConcurrentItems = limit;
            return this;
        }

        public ConnectionSettingsBuilder LimitAttemptsForOperationTo(int limit)
        {
            Ensure.Positive(limit, "limit");

            _maxAttempts = limit;
            return this;
        }

        public ConnectionSettingsBuilder LimitReconnectionsTo(int limit)
        {
            Ensure.Nonnegative(limit, "limit");

            _maxReconnections = limit;
            return this;
        }

        public ConnectionSettingsBuilder EnableOperationsForwarding()
        {
            _allowForwarding = true;
            return this;
        }

        public ConnectionSettingsBuilder DisableOperationsForwarding()
        {
            _allowForwarding = false;
            return this;
        }

        public ConnectionSettingsBuilder SetReconnectionDelayTo(TimeSpan reconnectionDelay)
        {
            _reconnectionDelay = reconnectionDelay;
            return this;
        }

        public ConnectionSettingsBuilder SetOperationTimeoutTo(TimeSpan operationTimeout)
        {
            _operationTimeout = operationTimeout;
            return this;
        }

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
