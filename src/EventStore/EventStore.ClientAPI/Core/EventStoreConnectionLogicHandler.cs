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

        private readonly SimpleQueuedHandler _queue = new SimpleQueuedHandler();

        private readonly Timer _timer;
        private readonly Stopwatch _timerStopwatch;

        private TcpPackageConnection _connection;
        private IPEndPoint _tcpEndPoint;
        private readonly Stopwatch _connectionStopwatch = new Stopwatch();
        private readonly Stopwatch _reconnectionStopwatch = new Stopwatch();
        private int _reconnectionCount;

        private bool _connectionActive;
        private bool _disposed;

        private readonly Dictionary<Guid, SubscriptionItem> _subscriptions = new Dictionary<Guid, SubscriptionItem>();
        private readonly List<SubscriptionItem> _retryPendingSubscriptions = new List<SubscriptionItem>();

        private readonly Dictionary<Guid, OperationItem> _operations = new Dictionary<Guid, OperationItem>();
        private readonly Queue<OperationItem> _waitingOperations = new Queue<OperationItem>();
        private readonly List<OperationItem> _retryPendingOperations = new List<OperationItem>();
        private int _operationCount;
        private int _operationsInProgressCount;

        public EventStoreConnectionLogicHandler(EventStoreConnection esConnection, ConnectionSettings settings)
        {
            Ensure.NotNull(esConnection, "esConnection");
            Ensure.NotNull(settings, "settings");

            _esConnection = esConnection;
            _settings = settings;
            _log = settings.Log;

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
            _log.Debug("EventStoreConnection '{0}': enqueueing message {1}.", _esConnection.ConnectionName, message);
            _queue.EnqueueMessage(message);
        }

        private void EstablishTcpConnection(TaskCompletionSource<object> task, IPEndPoint tcpEndPoint)
        {
            Ensure.NotNull(task, "task");
            Ensure.NotNull(tcpEndPoint, "tcpEndPoint");

            _log.Debug("EventStoreConnection '{0}': EstablishTcpConnection, tcpEndPoint: {1}.", _esConnection.ConnectionName, tcpEndPoint);

            if (_disposed)
            {
                task.SetException(new ObjectDisposedException(_esConnection.ConnectionName));
                return;
            }
            if (_connectionActive)
            {
                task.SetException(new InvalidOperationException(string.Format("EventStoreConnection '{0}' is already active.", _esConnection.ConnectionName)));
                return;
            }

            _connectionActive = true;
            _tcpEndPoint = tcpEndPoint;
            Connect();
            
            task.SetResult(null);
        }

        private void Connect()
        {
            _log.Info("EventStoreConnection '{0}': TCP connecting to [{1}]...", _esConnection.ConnectionName, _tcpEndPoint);
            
            _connection = new TcpPackageConnection(
                _log,
                _tcpEndPoint,
                Guid.NewGuid(),
                (connection, package) => EnqueueMessage(new HandleTcpPackageMessage(connection, package)),
                (connection, exc) => EnqueueMessage(new TcpConnectionErrorMessage(connection, exc)),
                connection => EnqueueMessage(new TcpConnectionEstablishedMessage(connection)),
                (connection, endpoint, error) => EnqueueMessage(new TcpConnectionClosedMessage(connection, error)));
            _connection.StartReceiving();
        }

        private void CloseConnection(string reason, Exception exception = null)
        {
            if (_disposed) return;

            _log.Debug("EventStoreConnection '{0}': CloseConnection, reason {1}, exception {2}.", _esConnection.ConnectionName, reason, exception);

            if (exception != null && _settings.ErrorOccurred != null)
                _settings.ErrorOccurred(_esConnection, exception);

            _connectionActive = false;
            _disposed = true;

            CloseTcpConnection();
            _timer.Dispose();

            foreach (var operation in _operations.Values.Concat(_waitingOperations).Concat(_retryPendingOperations))
            {
                operation.Operation.Fail(new ConnectionClosingException(string.Format("Connection '{0}' was closed.", _esConnection.ConnectionName)));
            }
            _operations.Clear();
            _waitingOperations.Clear();
            _retryPendingOperations.Clear();
            _operationCount = 0;
            _operationsInProgressCount = 0;

            foreach (var subscription in _subscriptions.Values.Concat(_retryPendingSubscriptions))
            {
                subscription.Operation.Fail(new ConnectionClosingException(string.Format("Connection '{0}' was closed.", _esConnection.ConnectionName)));
            }
            _subscriptions.Clear();
            _retryPendingSubscriptions.Clear();

            if (_settings.Closed != null)
                _settings.Closed(_esConnection, reason);

            _log.Info("EventStoreConnection '{0}': closed. Reason: {1}.", _esConnection.ConnectionName, reason);
        }

        private void CloseTcpConnection()
        {
            _log.Debug("EventStoreConnection '{0}': CloseTcpConnection.", _esConnection.ConnectionName);

            if (_connection == null) 
                return;

            _connection.Close();
            TcpConnectionClosed(_connection);
            _connection = null;
        }

        private void TcpConnectionEstablished(TcpPackageConnection connection)
        {
            if (_disposed || _connection != connection || connection.IsClosed)
                return;

            _log.Debug("EventStoreConnection '{0}': TCP connection to [{1}] established.", _esConnection.ConnectionName, connection.EffectiveEndPoint);

            _reconnectionStopwatch.Stop();
            _connectionStopwatch.Restart();

            if (_settings.Connected != null)
                _settings.Connected(_esConnection);
        }

        private void TcpConnectionClosed(TcpPackageConnection connection)
        {
            if (_disposed || _connection != connection)
                return;

            _log.Debug("EventStoreConnection '{0}': TCP connection to [{1}] closed.", _esConnection.ConnectionName, connection.EffectiveEndPoint);

            _connectionStopwatch.Stop();
            _reconnectionStopwatch.Restart();

            if (_settings.Disconnected != null)
                _settings.Disconnected(_esConnection);

            PurgeDroppedSubscriptions();
        }

        private void PurgeDroppedSubscriptions()
        {
            Ensure.NotNull(_connection, "_connection");

            var subscriptionsToRemove = new List<SubscriptionItem>();
            foreach (var subscription in _subscriptions.Values.Where(x => x.IsSubscribed && x.ConnectionId == _connection.ConnectionId))
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

            _log.Debug("EventStoreConnection '{0}': TimerTick.", _esConnection.ConnectionName);

            // operations timeouts are checked only if connection is established and check period time passed
            if (_connectionStopwatch.IsRunning && _connectionStopwatch.Elapsed >= _settings.OperationTimeoutCheckPeriod)
            {
                _log.Debug("EventStoreConnection '{0}': TimerTick checking timeouts...", _esConnection.ConnectionName);

                // On mono even impossible connection first says that it is established
                // so clearing of reconnection count on ConnectionEstablished event causes infinite reconnections.
                // So we reset reconnection count to zero on each timeout check period when connection is established
                _reconnectionCount = 0;
                CheckOperationsTimeouts();
                CheckSubscriptionsTimeouts();
                RetryPendingOperations();
                RetryPendingSubscriptions();

                _connectionStopwatch.Restart();
            }

            if (_reconnectionStopwatch.IsRunning && _reconnectionStopwatch.Elapsed >= _settings.ReconnectionDelay)
            {
                _log.Debug("EventStoreConnection '{0}': TimerTick checking reconnection...", _esConnection.ConnectionName);

                _reconnectionCount += 1;
                if (_settings.MaxReconnections >= 0 && _reconnectionCount > _settings.MaxReconnections)
                    CloseConnection("Reconnection limit reached.");
                else
                {
                    if (_settings.Reconnecting != null)
                        _settings.Reconnecting(_esConnection);
                    Connect();
                }
                _reconnectionStopwatch.Stop();
            }
        }

        private void CheckOperationsTimeouts()
        {
            Ensure.NotNull(_connection, "_connection");

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
                    var err = string.Format("EventStoreConnection '{0}': operation {1} never got response from server.\n" +
                                            "Last state update: {2:HH:mm:ss.fff}, UTC now: {3:HH:mm:ss.fff}.",
                                            _esConnection.ConnectionName, operation, operation.LastUpdated, DateTime.UtcNow);
                    _log.Error(err);

                    operation.Operation.Fail(new OperationTimedOutException(err));
                    removeOperations.Add(operation);
                }
            }

            foreach (var operation in retryOperations)
            {
                RetryOperation(operation);
            }
            foreach (var operation in removeOperations)
            {
                RemoveOperation(operation);
            }
        }

        private void RetryPendingOperations()
        {
            if (_retryPendingOperations.Count > 0)
            {
                _retryPendingOperations.Sort(SeqNoComparer);
                foreach (var operation in _retryPendingOperations)
                {
                    _log.Debug("EventStoreConnection '{0}': retrying {1}.", _esConnection.ConnectionName, operation);

                    operation.CorrelationId = Guid.NewGuid();
                    operation.RetryCount += 1;
                    ScheduleOperation(operation);
                }
                _retryPendingOperations.Clear();
            }
        }

        private void CheckSubscriptionsTimeouts()
        {
            Ensure.NotNull(_connection, "_connection");

            var retrySubscriptions = new List<SubscriptionItem>();
            var removeSubscriptions = new List<SubscriptionItem>();
            foreach (var subscription in _subscriptions.Values.Where(x => !x.IsSubscribed))
            {
                if (subscription.ConnectionId != _connection.ConnectionId)
                {
                    retrySubscriptions.Add(subscription);
                }
                else if (subscription.Timeout > TimeSpan.Zero && DateTime.UtcNow - subscription.LastUpdated > _settings.OperationTimeout)
                {
                    var err = string.Format("EventStoreConnection '{0}': subscription {1} never got confirmation from server.\n" +
                                            "Last state update: {2:HH:mm:ss.fff}, UTC now: {3:HH:mm:ss.fff}.",
                                            _esConnection.ConnectionName, subscription, subscription.LastUpdated, DateTime.UtcNow);
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

        private void RetryPendingSubscriptions()
        {
            if (_retryPendingSubscriptions.Count > 0)
            {
                foreach (var subscription in _retryPendingSubscriptions)
                {
                    subscription.RetryCount += 1;
                    StartSubscription(subscription);
                }
                _retryPendingSubscriptions.Clear();
            }
        }

        private void StartOperation(IClientOperation operation, int maxRetries, TimeSpan timeout)
        {
            if (_disposed)
            {
                operation.Fail(new ObjectDisposedException(_esConnection.ConnectionName));
                return;
            }
            if (!_connectionActive)
            {
                operation.Fail(new InvalidOperationException(string.Format("EventStoreConnection '{0}' is not active.", _esConnection.ConnectionName)));
                return;
            }
            _log.Debug("EventStoreConnection '{0}': StartOperation {1}, {2}, {3}, {4}.", _esConnection.ConnectionName, operation.GetType().Name, operation, maxRetries, timeout);

            ScheduleOperation(new OperationItem(operation, maxRetries, timeout));
        }

        private void StartSubscription(StartSubscriptionMessage msg)
        {
            if (_disposed)
            {
                msg.Source.SetException(new ObjectDisposedException(_esConnection.ConnectionName));
                return;
            }
            if (!_connectionActive)
            {
                msg.Source.SetException(new InvalidOperationException(string.Format("EventStoreConnection '{0}' is not active.", _esConnection.ConnectionName)));
                return;
            }

            var operation = new SubscriptionOperation(_log,
                                                      msg.Source,
                                                      x =>
                                                      {
                                                          if (_connection != null)
                                                              _connection.EnqueueSend(x);
                                                      },
                                                      msg.StreamId,
                                                      msg.ResolveLinkTos,
                                                      msg.EventAppeared,
                                                      msg.SubscriptionDropped);
            _log.Debug("EventStoreConnection '{0}': handling StartSubscriptionMessage. {1}, {2}, {3}, {4}.", _esConnection.ConnectionName, operation.GetType().Name, operation, msg.MaxRetries, msg.Timeout);
            StartSubscription(new SubscriptionItem(operation, msg.MaxRetries, msg.Timeout));
        }

        private void HandleTcpPackage(TcpPackageConnection connection, TcpPackage package)
        {
            if (_disposed || _connection != connection)
            {
                _log.Debug("EventStoreConnection '{0}': HandleTcpPackage IGNORED connId {1}, package {2}, {3}.", _esConnection.ConnectionName, connection.ConnectionId, package.Command, package.CorrelationId);
                return;
            }

            _log.Debug("EventStoreConnection '{0}': HandleTcpPackage connId {1}, package {2}, {3}.", _esConnection.ConnectionName, connection.ConnectionId, package.Command, package.CorrelationId);

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

            OperationItem operation;
            SubscriptionItem subscription;
            if (_operations.TryGetValue(package.CorrelationId, out operation))
            {
                var result = operation.Operation.InspectPackage(package);
                _log.Debug("EventStoreConnection '{0}': HandleTcpPackage OPERATION DECISION {1}, {2}.", _esConnection.ConnectionName, result.Decision, operation);
                switch (result.Decision)
                {
                    case InspectionDecision.DoNothing:
                        break;
                    case InspectionDecision.EndOperation:
                        RemoveOperation(operation);
                        break;
                    case InspectionDecision.Retry:
                        RetryOperation(operation);
                        break;
                    case InspectionDecision.Reconnect:
                        ReconnectAndRetryOperation(operation, result.TcpEndPoint);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(string.Format("Unknown InspectionDecision: {0}.", result.Decision));
                }
            } 
            else if (_subscriptions.TryGetValue(package.CorrelationId, out subscription))
            {
                var result = subscription.Operation.InspectPackage(package);
                _log.Debug("EventStoreConnection '{0}': HandleTcpPackage SUBSCRIPTION DECISION {1}, {2}.", _esConnection.ConnectionName, result.Decision, subscription);
                switch (result.Decision)
                {
                    case InspectionDecision.DoNothing: 
                        break;
                    case InspectionDecision.EndOperation:
                        RemoveSubscription(subscription);
                        break;
                    case InspectionDecision.Retry:
                        RetrySubscription(subscription);
                        break;
                    case InspectionDecision.Reconnect:
                        ReconnectAndRetrySubscription(subscription, result.TcpEndPoint);
                        break;
                    case InspectionDecision.Subscribed:
                        subscription.IsSubscribed = true;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(string.Format("Unknown InspectionDecision: {0}.", result.Decision));
                }
            }
        }

        private void TcpConnectionError(TcpPackageConnection connection, Exception exception)
        {
            if (_disposed || _connection != connection) 
                return;

            _log.Debug("EventStoreConnection '{0}': TcpConnectionError connId {1}, exc {2}.", _esConnection.ConnectionName, connection.ConnectionId, exception);

            CloseConnection("Exception occurred.", exception);
        }

        private bool RemoveOperation(OperationItem operation)
        {
            if (!_operations.Remove(operation.CorrelationId))
            {
                _log.Debug("EventStoreConnection '{0}': RemoveOperation FAILED for {1}.", _esConnection.ConnectionName, operation);
                return false;
            }

            _log.Debug("EventStoreConnection '{0}': RemoveOperation for {1}.", _esConnection.ConnectionName, operation);

            _operationsInProgressCount -= 1;
            _operationCount -= 1;
            
            while (_waitingOperations.Count > 0 && _operationsInProgressCount < _settings.MaxConcurrentItems)
            {
                ScheduleOperation(_waitingOperations.Dequeue());
            }
            return true;
        }

        private void RetryOperation(OperationItem operation)
        {
            if (!RemoveOperation(operation))
                return;

            _log.Debug("EventStoreConnection '{0}': RetryOperation for {1}.", _esConnection.ConnectionName, operation);

            if (operation.MaxRetries >= 0 && operation.RetryCount >= operation.MaxRetries)
            {
                operation.Operation.Fail(new RetriesLimitReachedException(operation.ToString(), operation.RetryCount));
                return;
            }

            _retryPendingOperations.Add(operation);
        }

        private void ReconnectAndRetryOperation(OperationItem operationItem, IPEndPoint tcpEndPoint)
        {
            Ensure.NotNull(tcpEndPoint, "tcpEndPoint");

            if (!_reconnectionStopwatch.IsRunning || !_tcpEndPoint.Equals(tcpEndPoint))
            {
                _log.Info("EventStoreConnection '{0}': going to reconnect to [{1}]. Current state: {2}, Current endpoint: {3}.",
                          _esConnection.ConnectionName, tcpEndPoint, 
                          _reconnectionStopwatch.IsRunning ? "reconnecting" : "connected", _tcpEndPoint);

                CloseTcpConnection();
                _tcpEndPoint = tcpEndPoint;
                Connect();
            }
            RetryOperation(operationItem);
        }

        private void ScheduleOperation(OperationItem operation)
        {
            Ensure.NotNull(_connection, "_connection");

            if (_operationsInProgressCount >= _settings.MaxConcurrentItems)
            {
                _log.Debug("EventStoreConnection '{0}': ScheduleOperation WAITING for {1}.", _esConnection.ConnectionName, operation);

                _waitingOperations.Enqueue(operation);
                return;
            }

            _operationsInProgressCount += 1;

            operation.ConnectionId = _connection.ConnectionId;
            operation.LastUpdated = DateTime.UtcNow;

            _operations.Add(operation.CorrelationId, operation);
            _operationCount += 1;

            var package = operation.Operation.CreateNetworkPackage(operation.CorrelationId);

            _log.Debug("EventStoreConnection '{0}': ScheduleOperation package {1}, {2}, {3}.", _esConnection.ConnectionName, package.Command, package.CorrelationId, operation);

            _connection.EnqueueSend(package);
        }

        private bool RemoveSubscription(SubscriptionItem subscription)
        {
            var res = _subscriptions.Remove(subscription.CorrelationId);
            _log.Debug("EventStoreConnection '{0}': RemoveSubscription {1}, result {2}.", _esConnection.ConnectionName, subscription, res);
            return res;
        }

        private void RetrySubscription(SubscriptionItem subscription)
        {
            if (!RemoveSubscription(subscription))
            {
                _log.Debug("EventStoreConnection '{0}': RemoveSubscription failed when trying to retry {1}.", _esConnection.ConnectionName, subscription);
                return;
            }

            if (subscription.MaxRetries >= 0 && subscription.RetryCount >= subscription.MaxRetries)
            {
                _log.Debug("EventStoreConnection '{0}': RETRIES LIMIT REACHED when trying to retry {1}.", _esConnection.ConnectionName, subscription);
                subscription.Operation.Fail(new RetriesLimitReachedException(subscription.ToString(), subscription.RetryCount));
                return;
            }

            _log.Debug("EventStoreConnection '{0}': retrying subscription {1}.", _esConnection.ConnectionName, subscription);
            _retryPendingSubscriptions.Add(subscription);
        }

        private void ReconnectAndRetrySubscription(SubscriptionItem operationItem, IPEndPoint tcpEndPoint)
        {
            Ensure.NotNull(tcpEndPoint, "tcpEndPoint");

            if (!_reconnectionStopwatch.IsRunning || !_tcpEndPoint.Equals(tcpEndPoint))
            {
                _log.Info("EventStoreConnection '{0}': going to reconnect to [{1}]. Current state: {2}, Current endpoint: {3}.",
                          _esConnection.ConnectionName, tcpEndPoint, 
                          _reconnectionStopwatch.IsRunning ? "reconnecting" : "connected", _tcpEndPoint);

                CloseTcpConnection();
                _tcpEndPoint = tcpEndPoint;
                Connect();
            }
            RetrySubscription(operationItem);
        }

        private void StartSubscription(SubscriptionItem subscription)
        {
            Ensure.NotNull(_connection, "_connection");

            if (subscription.IsSubscribed)
            {
                _log.Debug("EventStoreConnection '{0}': StartSubscription REMOVING due to already subscribed {1}.", _esConnection.ConnectionName, subscription);
                RemoveSubscription(subscription);
                return;
            }

            subscription.CorrelationId = Guid.NewGuid();
            subscription.ConnectionId = _connection.ConnectionId;
            subscription.LastUpdated = DateTime.UtcNow;

            _subscriptions.Add(subscription.CorrelationId, subscription);

            if (!subscription.Operation.Subscribe(subscription.CorrelationId))
            {
                _log.Debug("EventStoreConnection '{0}': StartSubscription REMOVING AS COULDN'T SUBSCRIBE {1}.", _esConnection.ConnectionName, subscription);
                RemoveSubscription(subscription);
            }
            else
            {
                _log.Debug("EventStoreConnection '{0}': StartSubscription SUBSCRIBING {1}.", _esConnection.ConnectionName, subscription);
            }
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
