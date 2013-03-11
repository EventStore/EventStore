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
using System.Linq;

namespace EventStore.ClientAPI.Core
{
    internal class EventStoreConnectionLogicHandler
    {
        private static readonly IComparer<OperationItem> SeqNoComparer = new OperationItemSeqNoComparer();

        public int TotalOperationCount { get { return _operationCount; } }

        private readonly EventStoreConnection _esConnection;
        private readonly ConnectionSettings _settings;
        private readonly ILogger _log;
        private readonly string _connectionName;

        private readonly SimpleQueuedHandler _queue = new SimpleQueuedHandler();

        private readonly Timer _timer;
        private readonly Stopwatch _timerStopwatch;

        private TcpPackageConnection _connection;
        private readonly Stopwatch _connectionStopwatch = new Stopwatch();
        private TimeSpan _lastTimeoutCheckTimestamp;

        private readonly Stopwatch _reconnectionStopwatch = new Stopwatch();
        private int _reconnectionCount;

        private bool _connectionActive;
        private volatile bool _disposed;

        private readonly Dictionary<Guid, SubscriptionItem> _subscriptions = new Dictionary<Guid, SubscriptionItem>();

        private readonly Dictionary<Guid, OperationItem> _operations = new Dictionary<Guid, OperationItem>();
        private readonly Queue<OperationItem> _waitingOperations = new Queue<OperationItem>();
        private int _operationCount;
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
            _queue.RegisterHandler<HandleTcpPackageMessage>(msg => HandleTcpPackage(msg.Connection, msg.Package));
            _queue.RegisterHandler<TcpConnectionErrorMessage>(msg => TcpConnectionError(msg.Connection, msg.Exception));
            _queue.RegisterHandler<TcpConnectionClosedMessage>(msg => TcpConnectionClosed(msg.Connection));

            _queue.RegisterHandler<TimerTickMessage>(msg => TimerTick());

            _queue.RegisterHandler<StartOperationMessage>(msg => StartOperation(msg.Operation, msg.MaxRetries, msg.Timeout));
            _queue.RegisterHandler<StartSubscriptionMessage>(StartSubscription);

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

            if (_disposed)
            {
                task.SetException(new ObjectDisposedException(_connectionName));
                return;
            }
            if (_connectionActive)
            {
                task.SetException(new InvalidOperationException("EventStoreConnection is already active."));
                return;
            }

            _settings.Log.Info("EventStoreConnection '{0}': connecting to [{1}].", _connectionName, tcpEndPoint);

            _connectionActive = true;
            Connect(tcpEndPoint);
            
            task.SetResult(null);
        }

        private void Connect(IPEndPoint tcpEndPoint)
        {
            Ensure.NotNull(tcpEndPoint, "tcpEndPoint");
            _connection = new TcpPackageConnection(
                _log,
                tcpEndPoint,
                Guid.NewGuid(),
                (connection, package) => _queue.EnqueueMessage(new HandleTcpPackageMessage(connection, package)),
                (connection, exc) => _queue.EnqueueMessage(new TcpConnectionErrorMessage(connection, exc)),
                connection => _queue.EnqueueMessage(new TcpConnectionEstablishedMessage(connection)),
                (connection, endpoint, error) => _queue.EnqueueMessage(new TcpConnectionClosedMessage(connection, error)));
            _connection.StartReceiving();
        }

        private void CloseConnection(string reason, Exception exception = null)
        {
            if (_disposed) return;

            if (exception != null && _settings.ErrorOccurred != null)
                _settings.ErrorOccurred(_esConnection, exception);

            _connectionActive = false;
            _disposed = true;

            CloseTcpConnection();
            _timer.Dispose();
            
            foreach (var operationItem in _operations)
            {
                operationItem.Value.Operation.Fail(new ConnectionClosingException(string.Format("Connection '{0}' was closed.", _connectionName)));
            }
            _operations.Clear();
            _operationCount = 0;
            _waitingOperations.Clear();

            foreach (var subscription in _subscriptions)
            {
                subscription.Value.Operation.Fail(new ConnectionClosingException(string.Format("Connection '{0}' was closed.", _connectionName)));
            }
            _subscriptions.Clear();

            if (_settings.Closed != null)
                _settings.Closed(_esConnection, reason);

            _log.Info("EventStoreConnection '{0}' closed.", _connectionName);
        }

        private void CloseTcpConnection()
        {
            if (_connection != null)
            {
                _connection.Close();
                TcpConnectionClosed(_connection);
            }
        }

        private void TcpConnectionEstablished(TcpPackageConnection connection)
        {
            if (_disposed || connection.ConnectionId != _connection.ConnectionId)
                return;

            _reconnectionStopwatch.Stop();
            _connectionStopwatch.Restart();

            if (_settings.Connected != null)
                _settings.Connected(_esConnection);
        }

        private void TcpConnectionClosed(TcpPackageConnection connection)
        {
            if (_disposed || connection.ConnectionId != _connection.ConnectionId)
                return;

            _connectionStopwatch.Stop();
            _reconnectionStopwatch.Restart();

            if (_settings.Disconnected != null)
                _settings.Disconnected(_esConnection);

            var subscriptionsToRemove = new List<SubscriptionItem>();
            foreach (var subscription in _subscriptions.Values.Where(x => x.IsSubscribed && x.ConnectionId == connection.ConnectionId))
            {
                subscription.Operation.ConnectionClosed();
                subscriptionsToRemove.Add(subscription);
            }
            foreach (var subscription in subscriptionsToRemove)
            {
                _subscriptions.Remove(subscription.CorrelationId);
            }
        }

        private void TimerTick()
        {
            if (_disposed || !_connectionActive) return;

            // operations timeouts are checked only if connection is established and check period time passed
            if (_connectionStopwatch.IsRunning && _connectionStopwatch.Elapsed - _lastTimeoutCheckTimestamp > _settings.OperationTimeoutCheckPeriod)
            {
                // On mono even impossible connection first says that it is established
                // so clearing of reconnection count on ConnectionEstablished event causes infinite reconnections.
                // So we reset reconnection count to zero on each timeout check period when connection is established
                _reconnectionCount = 0;
                CheckOperationsTimeouts();
                CheckSubscriptionsTimeouts();
                _lastTimeoutCheckTimestamp = _connectionStopwatch.Elapsed;
            }

            if (_reconnectionStopwatch.IsRunning && _reconnectionStopwatch.Elapsed >= _settings.ReconnectionDelay)
            {
                _reconnectionCount += 1;
                if (_settings.MaxReconnections >= 0 && _reconnectionCount > _settings.MaxReconnections)
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

        private void CheckOperationsTimeouts()
        {
            var retryOperations = new List<OperationItem>();
            var removeOperations = new List<OperationItem>();
            foreach (var operation in _operations.Values)
            {
                if (operation.ConnectionId != _connection.ConnectionId)
                {
                    retryOperations.Add(operation);
                }
                else if (operation.Timeout > TimeSpan.Zero && DateTime.UtcNow - operation.LastUpdated > _settings.OperationTimeout)
                {
                    var err = string.Format("{0} never got response from server.\n" +
                                            "Last state update: {1:HH:mm:ss.fff}, UTC now: {2:HH:mm:ss.fff}.",
                                            operation,
                                            operation.LastUpdated,
                                            DateTime.UtcNow);
                    _log.Error(err);

                    operation.Operation.Fail(new OperationTimedOutException(err));
                    removeOperations.Add(operation);
                }
            }

            retryOperations.Sort(SeqNoComparer);
            foreach (var operationItem in retryOperations)
            {
                RetryOperation(operationItem);
            }
            foreach (var operationItem in retryOperations)
            {
                RemoveOperation(operationItem);
            }
        }

        private void CheckSubscriptionsTimeouts()
        {
            var retrySubscriptions = new List<SubscriptionItem>();
            var removeSubscriptions = new List<SubscriptionItem>();
            foreach (var subscription in _subscriptions.Values.Where(x => x.IsSubscribed))
            {
                if (subscription.ConnectionId != _connection.ConnectionId)
                {
                    retrySubscriptions.Add(subscription);
                }
                else if (subscription.Timeout > TimeSpan.Zero && DateTime.UtcNow - subscription.LastUpdated > _settings.OperationTimeout)
                {
                    var err = string.Format("Subscription {0} never got confirmation from server.\n" +
                                            "Last state update: {1:HH:mm:ss.fff}, UTC now: {2:HH:mm:ss.fff}.",
                                            subscription,
                                            subscription.LastUpdated,
                                            DateTime.UtcNow);
                    _log.Error(err);

                    subscription.Operation.Fail(new OperationTimedOutException(err));
                    removeSubscriptions.Add(subscription);
                }
            }

            foreach (var subscription in retrySubscriptions)
            {
                RetrySubscription(subscription);
            }
            foreach (var subscription in removeSubscriptions)
            {
                RemoveSubscription(subscription);
            }
        }

        private void StartOperation(IClientOperation operation, int maxRetries, TimeSpan timeout)
        {
            if (_disposed)
            {
                operation.Fail(new ObjectDisposedException(_connectionName));
                return;
            }
            if (!_connectionActive)
            {
                operation.Fail(new InvalidOperationException(string.Format("EventStoreConnection '{0}' is not active.", _connectionName)));
                return;
            }

            ScheduleOperation(new OperationItem(operation, maxRetries, timeout));
        }

        private void StartSubscription(StartSubscriptionMessage msg)
        {
            if (_disposed)
            {
                msg.Source.SetException(new ObjectDisposedException(_connectionName));
                return;
            }
            if (!_connectionActive)
            {
                msg.Source.SetException(new InvalidOperationException(string.Format("EventStoreConnection '{0}' is not active.", _connectionName)));
                return;
            }

            var subscription = new SubscriptionItem(new SubscriptionOperation(_log,
                                                                              msg.Source,
                                                                              x => _connection.EnqueueSend(x),
                                                                              msg.StreamId,
                                                                              msg.ResolveLinkTos,
                                                                              msg.EventAppeared,
                                                                              msg.SubscriptionDropped),
                                                    msg.MaxRetries,
                                                    msg.Timeout);
            StartSubscription(subscription);
        }

        private void HandleTcpPackage(TcpPackageConnection connection, TcpPackage package)
        {
            if (_disposed || connection.ConnectionId != _connection.ConnectionId) 
                return;

            if (package.Command == TcpCommand.BadRequest && package.CorrelationId == Guid.Empty)
            {
                if (_settings.ErrorOccurred != null)
                {
                    string message = Helper.EatException(() => Encoding.UTF8.GetString(package.Data.Array, package.Data.Offset, package.Data.Count));
                    var exc = new EventStoreConnectionException(
                        string.Format("BadRequest received from server. Error: {0}", string.IsNullOrEmpty(message) ? "<no message>" : message));
                    CloseConnection("Connection-wide BadRequest received. Too dangerous to continue.", exc);
                }
                return;
            }

            OperationItem operationItem;
            SubscriptionItem subscriptionItem;
            if (_operations.TryGetValue(package.CorrelationId, out operationItem))
            {
                var result = operationItem.Operation.InspectPackage(package);
                switch (result.Decision)
                {
                    case InspectionDecision.DoNothing:
                        break;
                    case InspectionDecision.EndOperation:
                        RemoveOperation(operationItem);
                        break;
                    case InspectionDecision.Retry:
                        RetryOperation(operationItem);
                        break;
                    case InspectionDecision.Reconnect:
                        ReconnectAndRetryOperation(operationItem, result.TcpEndPoint);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(string.Format("Unknown InspectionDecision: {0}.", result.Decision));
                }
            } 
            else if (_subscriptions.TryGetValue(package.CorrelationId, out subscriptionItem))
            {
                var result = subscriptionItem.Operation.InspectPackage(package);
                switch (result.Decision)
                {
                    case InspectionDecision.DoNothing: 
                        break;
                    case InspectionDecision.EndOperation:
                        RemoveSubscription(subscriptionItem);
                        break;
                    case InspectionDecision.Retry:
                        RetrySubscription(subscriptionItem);
                        break;
                    case InspectionDecision.Reconnect:
                        ReconnectAndRetrySubscription(subscriptionItem, result.TcpEndPoint);
                        break;
                    case InspectionDecision.Subscribed:
                        subscriptionItem.IsSubscribed = true;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(string.Format("Unknown InspectionDecision: {0}.", result.Decision));
                }
            }
        }

        private void TcpConnectionError(TcpPackageConnection connection, Exception exception)
        {
            if (_disposed || connection.ConnectionId != _connection.ConnectionId) 
                return;

            CloseConnection("Exception occurred.", exception);
        }

        private bool RemoveOperation(OperationItem operation)
        {
            if (!_operations.Remove(operation.CorrelationId))
                return false;

            _operationsInProgressCount -= 1;
            _operationCount -= 1;
            
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

                CloseTcpConnection();
                Connect(tcpEndPoint);
            }
            RetryOperation(operationItem);
        }

        private void ScheduleOperation(OperationItem operation)
        {
            if (_operationsInProgressCount >= _settings.MaxConcurrentItems)
            {
                _waitingOperations.Enqueue(operation);
                return;
            }

            _operationsInProgressCount += 1;

            operation.ConnectionId = _connection.ConnectionId;
            operation.LastUpdated = DateTime.UtcNow;

            _operations.Add(operation.CorrelationId, operation);
            _operationCount += 1;

            var package = operation.Operation.CreateNetworkPackage(operation.CorrelationId);
            _connection.EnqueueSend(package);
        }

        private bool RemoveSubscription(SubscriptionItem subscription)
        {
            return _subscriptions.Remove(subscription.CorrelationId);
        }

        private void RetrySubscription(SubscriptionItem subscription)
        {
            if (!RemoveSubscription(subscription))
                return;

            if (subscription.MaxRetries >= 0 && subscription.RetryCount >= subscription.MaxRetries)
            {
                subscription.Operation.Fail(new RetriesLimitReachedException(subscription.ToString(), subscription.RetryCount));
                return;
            }

            subscription.RetryCount += 1;
            StartSubscription(subscription);
        }

        private void ReconnectAndRetrySubscription(SubscriptionItem operationItem, IPEndPoint tcpEndPoint)
        {
            if (!_reconnectionStopwatch.IsRunning || !_connection.EffectiveEndPoint.Equals(tcpEndPoint))
            {
                _log.Info("Going to reconnect to [{0}]. Current state: {1}, Current endpoint: {2}",
                          tcpEndPoint, _reconnectionStopwatch.IsRunning ? "reconnecting" : "connected", _connection.EffectiveEndPoint);

                CloseTcpConnection();
                Connect(tcpEndPoint);
            }
            RetrySubscription(operationItem);
        }

        private void StartSubscription(SubscriptionItem subscription)
        {
            if (subscription.IsSubscribed)
            {
                RemoveSubscription(subscription);
                return;
            }

            subscription.CorrelationId = Guid.NewGuid();
            subscription.ConnectionId = _connection.ConnectionId;
            subscription.LastUpdated = DateTime.UtcNow;

            _subscriptions.Add(subscription.CorrelationId, subscription);

            if (!subscription.Operation.Subscribe(subscription.CorrelationId))
                RemoveSubscription(subscription);
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

                CorrelationId = Guid.NewGuid();
                RetryCount = 0;
                LastUpdated = DateTime.UtcNow;
            }

            public override string ToString()
            {
                return string.Format("Operation {0} ({1:B}): {2}, retry count: {3}, last updated: {4:HH:mm:ss.fff}",
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

        public class SubscriptionItem
        {
            public readonly SubscriptionOperation Operation;
            public readonly int MaxRetries;
            public readonly TimeSpan Timeout;

            public Guid ConnectionId;
            public Guid CorrelationId;
            public bool IsSubscribed;
            public int RetryCount;
            public DateTime LastUpdated;

            public SubscriptionItem(SubscriptionOperation operation, int maxRetries, TimeSpan timeout)
            {
                Ensure.NotNull(operation, "operation");

                Operation = operation;
                MaxRetries = maxRetries;
                Timeout = timeout;

                CorrelationId = Guid.NewGuid();
                RetryCount = 0;
                LastUpdated = DateTime.UtcNow;
            }

            public override string ToString()
            {
                return string.Format("Subscription {0} ({1:B}): {2}, is subscribed: {3}, retry count: {4}, last updated: {5:HH:mm:ss.fff}",
                                     Operation.GetType().Name,
                                     CorrelationId,
                                     Operation,
                                     IsSubscribed,
                                     RetryCount,
                                     LastUpdated);
            }
        }
    }
}
