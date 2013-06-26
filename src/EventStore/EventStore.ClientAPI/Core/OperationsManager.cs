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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using EventStore.ClientAPI.ClientOperations;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.Transport.Tcp;

namespace EventStore.ClientAPI.Core
{
    internal class OperationItem
    {
        private static long _nextSeqNo = -1;
        public readonly long SeqNo = Interlocked.Increment(ref _nextSeqNo);

        public readonly IClientOperation Operation;
        public readonly int MaxRetries;
        public readonly TimeSpan Timeout;
        public readonly DateTime CreatedTime;

        public Guid ConnectionId;
        public Guid CorrelationId;
        public int RetryCount;
        public DateTime LastUpdated;

        public OperationItem(IClientOperation operation, int maxRetries, TimeSpan timeout)
        {
            Ensure.NotNull(operation, "operation");

            Operation = operation;
            MaxRetries = maxRetries;
            Timeout = timeout;
            CreatedTime = DateTime.UtcNow;

            CorrelationId = Guid.NewGuid();
            RetryCount = 0;
            LastUpdated = DateTime.UtcNow;
        }

        public override string ToString()
        {
            return string.Format("Operation {0} ({1:B}): {2}, retry count: {3}, created: {4:HH:mm:ss.fff}, last updated: {5:HH:mm:ss.fff}",
                                 Operation.GetType().Name, CorrelationId, Operation, RetryCount, CreatedTime, LastUpdated);
        }
    }

    internal class OperationsManager
    {
        private static readonly IComparer<OperationItem> SeqNoComparer = new OperationItemSeqNoComparer();

        public int TotalOperationCount { get { return _totalOperationCount; } }

        private readonly string _connectionName;
        private readonly ConnectionSettings _settings;
        private readonly Dictionary<Guid, OperationItem> _activeOperations = new Dictionary<Guid, OperationItem>();
        private readonly Queue<OperationItem> _waitingOperations = new Queue<OperationItem>();
        private readonly List<OperationItem> _retryPendingOperations = new List<OperationItem>();

        private int _totalOperationCount;

        public OperationsManager(string connectionName, ConnectionSettings settings)
        {
            Ensure.NotNull(connectionName, "connectionName");
            Ensure.NotNull(settings, "settings");
            _connectionName = connectionName;
            _settings = settings;
        }

        public bool TryGetActiveOperation(Guid correlationId, out OperationItem operation)
        {
            return _activeOperations.TryGetValue(correlationId, out operation);
        }

        public void CleanUp()
        {
            var connectionClosedException = new ConnectionClosedException(string.Format("Connection '{0}' was closed.", _connectionName));
            foreach (var operation in _activeOperations.Values
                                      .Concat(_waitingOperations)
                                      .Concat(_retryPendingOperations))
            {
                operation.Operation.Fail(connectionClosedException);
            }
            _activeOperations.Clear();
            _waitingOperations.Clear();
            _retryPendingOperations.Clear();
            _totalOperationCount = 0;
        }

        public void CheckTimeoutsAndRetry(TcpPackageConnection connection)
        {
            Ensure.NotNull(connection, "connection");

            var retryOperations = new List<OperationItem>();
            var removeOperations = new List<OperationItem>();
            foreach (var operation in _activeOperations.Values)
            {
                if (operation.ConnectionId != connection.ConnectionId)
                {
                    retryOperations.Add(operation);
                }
                else if (operation.Timeout > TimeSpan.Zero && DateTime.UtcNow - operation.LastUpdated > _settings.OperationTimeout)
                {
                    var err = string.Format("EventStoreConnection '{0}': operation never got response from server.\n"
                                            + "UTC now: {1:HH:mm:ss.fff}, operation: {2}.",
                                            _connectionName, DateTime.UtcNow, operation);
                    _settings.Log.Error(err);

                    if (_settings.FailOnNoServerResponse)
                    {
                        operation.Operation.Fail(new OperationTimedOutException(err));
                        removeOperations.Add(operation);
                    }
                    else
                    {
                        retryOperations.Add(operation);
                    }
                }
            }

            foreach (var operation in retryOperations)
            {
                ScheduleOperationRetry(operation);
            }
            foreach (var operation in removeOperations)
            {
                RemoveOperation(operation);
            }

            if (_retryPendingOperations.Count > 0)
            {
                _retryPendingOperations.Sort(SeqNoComparer);
                foreach (var operation in _retryPendingOperations)
                {
                    var oldCorrId = operation.CorrelationId;
                    operation.CorrelationId = Guid.NewGuid();
                    operation.RetryCount += 1;
                    LogDebug("retrying, old corrId {0}, operation {1}.", oldCorrId, operation);
                    ScheduleOperation(operation, connection);
                }
                _retryPendingOperations.Clear();
            }

            ScheduleWaitingOperations(connection);
        }

        public void ScheduleOperationRetry(OperationItem operation)
        {
            if (!RemoveOperation(operation))
                return;

            LogDebug("ScheduleOperationRetry for {0}", operation);
            if (operation.MaxRetries >= 0 && operation.RetryCount >= operation.MaxRetries)
            {
                operation.Operation.Fail(new RetriesLimitReachedException(operation.ToString(), operation.RetryCount));
                return;
            }
            _retryPendingOperations.Add(operation);
        }

        public bool RemoveOperation(OperationItem operation)
        {
            if (!_activeOperations.Remove(operation.CorrelationId))
            {
                LogDebug("RemoveOperation FAILED for {0}", operation);
                return false;
            }
            LogDebug("RemoveOperation SUCCEEDED for {0}", operation);
            _totalOperationCount = _activeOperations.Count + _waitingOperations.Count;
            return true;
        }

        public void ScheduleWaitingOperations(TcpPackageConnection connection)
        {
            Ensure.NotNull(connection, "connection");
            while (_waitingOperations.Count > 0 && _activeOperations.Count < _settings.MaxConcurrentItems)
            {
                ScheduleOperation(_waitingOperations.Dequeue(), connection);
            }
            _totalOperationCount = _activeOperations.Count + _waitingOperations.Count;
        }

        public void EnqueueOperation(OperationItem operation)
        {
            LogDebug("EnqueueOperation WAITING for {0}.", operation);
            _waitingOperations.Enqueue(operation);
        }

        public void ScheduleOperation(OperationItem operation, TcpPackageConnection connection)
        {
            Ensure.NotNull(connection, "connection");

            if (_activeOperations.Count >= _settings.MaxConcurrentItems)
            {
                LogDebug("ScheduleOperation WAITING for {0}.", operation);
                _waitingOperations.Enqueue(operation);
            }
            else
            {
                operation.ConnectionId = connection.ConnectionId;
                operation.LastUpdated = DateTime.UtcNow;
                _activeOperations.Add(operation.CorrelationId, operation);

                var package = operation.Operation.CreateNetworkPackage(operation.CorrelationId);
                LogDebug("ScheduleOperation package {0}, {1}, {2}.", package.Command, package.CorrelationId, operation);
                connection.EnqueueSend(package);
            }
            _totalOperationCount = _activeOperations.Count + _waitingOperations.Count;
        }

        private void LogDebug(string message, params object[] parameters)
        {
            if (_settings.VerboseLogging) _settings.Log.Debug("EventStoreConnection '{0}': {1}.", _connectionName, parameters.Length == 0 ? message : string.Format(message, parameters));
        }

        internal class OperationItemSeqNoComparer : IComparer<OperationItem>
        {
            public int Compare(OperationItem x, OperationItem y)
            {
                return x.SeqNo.CompareTo(y.SeqNo);
            }
        }
    }
}
