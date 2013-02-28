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
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI.ClientOperations;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.Transport.Tcp;

namespace EventStore.ClientAPI.Core
{
    internal class EventStoreConnectionLogicHandler
    {
        private static readonly IComparer<OperationItem> SeqNoComparer = new OperationItemSeqNoComparer();

        public int TotalOperationCount { get { return _operations.Count; } }

        private readonly SimpleQueuedHandler _queue = new SimpleQueuedHandler();

        private readonly Timer _timer;
        private readonly Stopwatch _timerStopwatch;

        private readonly EventStoreConnection _esConnection;
        private readonly ConnectionSettings _settings;
        private readonly ILogger _log;
        private readonly string _connectionName;

        private TcpTypedConnection _connection;
        private readonly Stopwatch _connectionStopwatch = new Stopwatch();
        private TimeSpan _lastTimeoutCheckTimestamp;

        private readonly Stopwatch _reconnectionStopwatch = new Stopwatch();
        private int _reconnectionCount;

        private bool _connectionActive;
        private bool _disposed;

        private readonly Dictionary<Guid, OperationItem> _operations = new Dictionary<Guid, OperationItem>();
        private readonly Queue<OperationItem> _waitingOperations = new Queue<OperationItem>();
        private volatile int _operationsInProgressCount;

        public EventStoreConnectionLogicHandler(EventStoreConnection esConnection, ConnectionSettings settings, string connectionName)
        {
            Ensure.NotNull(esConnection, "esConnection");
            Ensure.NotNull(settings, "settings");
            Ensure.NotNullOrEmpty(connectionName, "connectionName");

            _esConnection = esConnection;
            _settings = settings;
            _log = settings.Log;
            _connectionName = connectionName;

            _queue.RegisterHandler<EstablishTcpConnectionMessage>(msg => EstablishTcpConnection(msg.Task, msg.EndPoint));
            _queue.RegisterHandler<CloseConnectionMessage>(msg => CloseConnection("Connection close requested by client."));
            _queue.RegisterHandler<TcpConnectionEstablishedMessage>(msg => TcpConnectionEstablished(msg.Connection));
            _queue.RegisterHandler<TcpConnectionClosedMessage>(msg => TcpConnectionClosed(msg.Connection));

            _queue.RegisterHandler<TimerTickMessage>(TimerTick);

            _queue.RegisterHandler<StartOperationMessage>(msg => StartOperation(msg.Operation, msg.MaxAttempts, msg.Timeout));

            _queue.RegisterHandler<RemoveOperationMessage>(msg => RemoveOperation(msg.OperationItem));
            _queue.RegisterHandler<RetryOperationMessage>(msg => RetryOperation(msg.OperationItem));
            _queue.RegisterHandler<ReconnectAndRetryOperationMessage>(msg => ReconnectAndRetryOperation(msg.OperationItem, msg.TcpEndPoint));

            _timerStopwatch = Stopwatch.StartNew();
            _timer = new Timer(_ => _queue.EnqueueMessage(new TimerTickMessage(_timerStopwatch.Elapsed)),
                               null,
                               Consts.TimerPeriod,
                               Consts.TimerPeriod);
        }

        public void EnqueueMessage(Message message)
        {
            _queue.EnqueueMessage(message);
        }

        private void EstablishTcpConnection(TaskCompletionSource<object> task, IPEndPoint tcpEndPoint)
        {
            Ensure.NotNull(task, "task");
            Ensure.NotNull(tcpEndPoint, "tcpEndPoint");

            if (_disposed) task.SetException(new ObjectDisposedException(_connectionName));
            if (_connectionActive) task.SetException(new InvalidOperationException("EventStoreConnection is already active."));

            _settings.Log.Info("EventStoreConnection '{0}': connecting to [{1}].", _connectionName, tcpEndPoint);

            _connectionActive = true;
            Connect(tcpEndPoint);
            
            task.SetResult(null);
        }

        private void Connect(IPEndPoint tcpEndPoint)
        {
            Ensure.NotNull(tcpEndPoint, "tcpEndPoint");
            _connection = new TcpConnector(_log).CreateTcpConnection(
                tcpEndPoint,
                Guid.NewGuid(),
                HandleTcpPackage,
                connection => _queue.EnqueueMessage(new TcpConnectionEstablishedMessage(connection)),
                (connection, endpoint, error) => _queue.EnqueueMessage(new TcpConnectionClosedMessage(connection, error)));
        }

        private void CloseConnection(string reason)
        {
            if (_disposed) return;

            _connectionActive = false;
            _disposed = true;

            if (_connection != null)
                _connection.Close();
            
            _timer.Dispose();

            foreach (var operationItem in _operations)
            {
                operationItem.Value.Operation.Fail(
                    new ConnectionClosingException(string.Format("Operation was in progress on connection '{0}' closing.", _connectionName)));
            }
            _operations.Clear();
            _waitingOperations.Clear();
            
            _log.Info("EventStoreConnection '{0}' closed.", _connectionName);

            if (_settings.Closed != null)
                _settings.Closed(_esConnection, reason);
        }

        private void TcpConnectionEstablished(TcpTypedConnection connection)
        {
            if (_disposed || connection.ConnectionId != _connection.ConnectionId)
                return;

            _reconnectionStopwatch.Stop();
            _reconnectionCount = 0;
            _connectionStopwatch.Restart();

            if (_settings.Connected != null)
                _settings.Connected(_esConnection);
        }

        private void TcpConnectionClosed(TcpTypedConnection connection)
        {
            if (_disposed || connection.ConnectionId != _connection.ConnectionId)
                return;

            _connectionStopwatch.Stop();
            _reconnectionStopwatch.Restart();

            if (_settings.Disconnected != null)
                _settings.Disconnected(_esConnection);
        }

        private void TimerTick(TimerTickMessage msg)
        {
            if (_disposed || !_connectionActive) return;

            if (_connectionStopwatch.IsRunning && _connectionStopwatch.Elapsed - _lastTimeoutCheckTimestamp > _settings.OperationTimeoutCheckPeriod)
            {
                var retriable = new List<OperationItem>();
                foreach (var operation in _operations.Values)
                {
                    if (operation.ConnectionId != _connection.ConnectionId)
                    {
                        retriable.Add(operation);
                    }
                    else if (operation.Timeout > TimeSpan.Zero && DateTime.Now - operation.LastUpdated > _settings.OperationTimeout)
                    {
                        var err = string.Format("{0} never got response from server.\n" +
                                                "Last state update: {1:HH:mm:ss.fff}, UTC now: {2:HH:mm:ss.fff}.",
                                                operation, operation.LastUpdated, DateTime.Now);
                        _log.Error(err);

                        operation.Operation.Fail(new OperationTimedOutException(err));
                        _queue.EnqueueMessage(new RemoveOperationMessage(operation));
                    }
                }

                retriable.Sort(SeqNoComparer);
                foreach (var operationItem in retriable)
                {
                    _queue.EnqueueMessage(new RetryOperationMessage(operationItem));
                }

                _lastTimeoutCheckTimestamp = _connectionStopwatch.Elapsed;
            }

            if (_reconnectionStopwatch.IsRunning && _reconnectionStopwatch.Elapsed >= _settings.ReconnectionDelay)
            {
                _reconnectionCount += 1;
                if (_reconnectionCount > _settings.MaxReconnections)
                    CloseConnection("Reconnection limit reached.");
                else
                {
                    if (_settings.Reconnecting != null)
                        _settings.Reconnecting(_esConnection);
                    Connect(_connection.EffectiveEndPoint);
                }
                _reconnectionStopwatch.Stop();
            }
        }

        private void StartOperation(IClientOperation operation, int maxAttempts, TimeSpan timeout)
        {
            if (_disposed) operation.Fail(new ObjectDisposedException(_connectionName));
            if (!_connectionActive) operation.Fail(new InvalidOperationException(string.Format("EventStoreConnection '{0}' is not active.", _connectionName)));

            ScheduleOperation(new OperationItem(operation, maxAttempts, timeout));
        }

        private void HandleTcpPackage(TcpTypedConnection connection, TcpPackage package)
        {
            if (package.Command == TcpCommand.BadRequest && package.CorrelationId == Guid.Empty)
            {
                if (_settings.ErrorOccurred != null)
                {
                    string message = Helper.EatException(() => Encoding.UTF8.GetString(package.Data.Array, package.Data.Offset, package.Data.Count));
                    var exc = new EventStoreConnectionException(
                        string.Format("BadRequest received from server. Error: {0}", string.IsNullOrEmpty(message) ? "<no message>" : message));
                    _settings.ErrorOccurred(_esConnection, exc);
                }
                return;
            }

            OperationItem operationItem;
            if (!_operations.TryGetValue(package.CorrelationId, out operationItem))
            {
                _log.Debug("Unexpected CorrelationId {{{0}}} received. Ignoring...", package.CorrelationId);
                return;
            }

            var result = operationItem.Operation.InspectPackage(package);
            switch (result.Decision)
            {
                case InspectionDecision.DoNothing:
                {
                    break;
                }
                case InspectionDecision.EndOperation:
                {
                    _queue.EnqueueMessage(new RemoveOperationMessage(operationItem));
                    break;
                }
                case InspectionDecision.Retry:
                {
                    _queue.EnqueueMessage(new RetryOperationMessage(operationItem));
                    break;
                }
                case InspectionDecision.Reconnect:
                {
                    _queue.EnqueueMessage(new ReconnectAndRetryOperationMessage(operationItem, result.TcpEndPoint));
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException(string.Format("Unknown InspectionDecision: {0}.", result.Decision));
            }
        }

        private bool RemoveOperation(OperationItem operation)
        {
            if (!_operations.Remove(operation.CorrelationId))
                return false;

            _operationsInProgressCount -= 1;
            
            while (_operationsInProgressCount < _settings.MaxConcurrentItems && _waitingOperations.Count > 0)
            {
                var waitingOperation = _waitingOperations.Dequeue();
                ScheduleOperation(waitingOperation);
            }
            return true;
        }

        private void RetryOperation(OperationItem operation)
        {
            if (!RemoveOperation(operation))
                return;

            if (operation.MaxRetries >= 0 && operation.RetryCount >= operation.MaxRetries)
            {
                operation.Operation.Fail(new RetriesLimitReachedException(operation.ToString(), operation.RetryCount));
                return;
            }

            operation.CorrelationId = Guid.NewGuid();
            operation.RetryCount += 1;

            ScheduleOperation(operation);
        }

        private void ReconnectAndRetryOperation(OperationItem operationItem, IPEndPoint tcpEndPoint)
        {
            if (!_reconnectionStopwatch.IsRunning || !_connection.EffectiveEndPoint.Equals(tcpEndPoint))
            {
                _log.Info("Going to reconnect to [{0}]. Current state: {1}, Current endpoint: {2}",
                          tcpEndPoint, _reconnectionStopwatch.IsRunning ? "reconnecting" : "connected", _connection.EffectiveEndPoint);

                _connection.Close();
                Connect(tcpEndPoint);
            }
            RetryOperation(operationItem);
        }

        private void ScheduleOperation(OperationItem operation)
        {
            if (!operation.Operation.IsLongRunning && _operationsInProgressCount >= _settings.MaxConcurrentItems)
            {
                _waitingOperations.Enqueue(operation);
                return;
            }

            if (!operation.Operation.IsLongRunning)
                _operationsInProgressCount += 1;

            operation.ConnectionId = _connection.ConnectionId;
            operation.LastUpdated = DateTime.UtcNow;

            _operations.Add(operation.CorrelationId, operation);

            var package = operation.Operation.CreateNetworkPackage(operation.CorrelationId);
            _connection.EnqueueSend(package.AsByteArray());
        }

        public class OperationItem
        {
            private static long _nextSeqNo = -1;

            public readonly long SeqNo = Interlocked.Increment(ref _nextSeqNo);

            public readonly IClientOperation Operation;
            public readonly int MaxRetries;
            public readonly TimeSpan Timeout;

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

                RetryCount = 0;
                LastUpdated = DateTime.UtcNow;
            }

            public override string ToString()
            {
                return string.Format("WorkItem {0} ({1:B}): {2}, retry count: {3}, last updated: {4}",
                                     Operation.GetType().Name,
                                     CorrelationId,
                                     Operation,
                                     RetryCount,
                                     LastUpdated);
            }
        }

        private class OperationItemSeqNoComparer: IComparer<OperationItem>
        {
            public int Compare(OperationItem x, OperationItem y)
            {
                return x.SeqNo.CompareTo(y.SeqNo);
            }
        }
    }
}
