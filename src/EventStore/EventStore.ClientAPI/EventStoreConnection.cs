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
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI.ClientOperations;
using EventStore.ClientAPI.Common.Log;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Connection;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.Transport.Tcp;
using Ensure = EventStore.ClientAPI.Common.Utils.Ensure;
using System.Linq;

namespace EventStore.ClientAPI
{
    public class EventStoreConnection : IDisposable
    {
        private readonly ILogger _log;

        private IPEndPoint _tcpEndPoint;
        private readonly TcpConnector _connector;
        private TcpTypedConnection _connection;
        private readonly object _connectionLock = new object();
        private volatile bool _active;

        private readonly SubscriptionsChannel _subscriptionsChannel;

        private readonly Common.Concurrent.ConcurrentQueue<IClientOperation> _queue = new Common.Concurrent.ConcurrentQueue<IClientOperation>();
        private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, WorkItem> _inProgress = new System.Collections.Concurrent.ConcurrentDictionary<Guid, WorkItem>();
        private int _inProgressCount;

        private DateTime _lastReconnectionTimestamp;
        private readonly Stopwatch _reconnectionStopwatch = new Stopwatch();
        private readonly Stopwatch _timeoutCheckStopwatch = new Stopwatch();
        private int _reconnectionsCount;

        private Thread _worker;
        private volatile bool _stopping;

        private readonly ConnectionSettings _settings;

        private EventStoreConnection(ConnectionSettings settings)
        {
            _settings = settings;

            LogManager.RegisterLogger(settings.Log);
            _log = LogManager.GetLogger();

            _connector = new TcpConnector();
            _subscriptionsChannel = new SubscriptionsChannel(_connector);
        }

        public static EventStoreConnection Create()
        {
            return new EventStoreConnection(ConnectionSettings.Default);
        }

        public static EventStoreConnection Create(ConnectionSettings settings)
        {
            Ensure.NotNull(settings, "settings");
            return new EventStoreConnection(settings);
        }

        public void Connect(IPEndPoint tcpEndPoint)
        {
            ConnectAsync(tcpEndPoint).Wait();
        }

        public Task ConnectAsync(IPEndPoint tcpEndPoint)
        {
            Ensure.NotNull(tcpEndPoint, "tcpEndPoint");
            return EstablishConnectionAsync(tcpEndPoint);
        }

        public void Connect(string clusterDns, int maxDiscoverAttempts = 10, int port = 30777)
        {
            ConnectAsync(clusterDns, maxDiscoverAttempts, port).Wait();
        }

        public Task ConnectAsync(string clusterDns, int maxDiscoverAttempts = 10, int port = 30777)
        {
            Ensure.NotNullOrEmpty(clusterDns, "clusterDns");
            Ensure.Positive(maxDiscoverAttempts, "maxDiscoverAttempts");
            Ensure.Nonnegative(port, "port");

            var explorer = new ClusterExplorer(_settings.AllowForwarding, maxDiscoverAttempts, port);
            return explorer.Resolve(clusterDns)
                           .ContinueWith(resolve =>
                                         {
                                             if (resolve.IsFaulted || resolve.Result == null)
                                                 throw new CannotEstablishConnectionException("Failed to find node to connect", resolve.Exception);
                                             var endPoint = resolve.Result;
                                             return EstablishConnectionAsync(endPoint);
                                         });
        }

        private Task EstablishConnectionAsync(IPEndPoint tcpEndPoint)
        {
            lock (_connectionLock)
            {
                if (_active)
                    throw new InvalidOperationException("EventStoreConnection is already active");
                if (_stopping)
                    throw new InvalidOperationException("EventStoreConnection has been closed");
                _active = true;

                _tcpEndPoint = tcpEndPoint;

                _lastReconnectionTimestamp = DateTime.UtcNow;
                _connection = _connector.CreateTcpConnection(_tcpEndPoint, OnPackageReceived, OnConnectionEstablished, OnConnectionClosed);
                _timeoutCheckStopwatch.Start();

                _worker = new Thread(MainLoop) {IsBackground = true, Name = "Worker thread"};
                _worker.Start();

                return Tasks.CreateCompleted();
            }
        }

        private void EnsureActive()
        {
            if (!_active)
                throw new InvalidOperationException(string.Format("EventStoreConnection [{0}] is not active.", _tcpEndPoint));
        }

        void IDisposable.Dispose()
        {
            Close();
        }

        public void Close()
        {
            lock (_connectionLock)
            {
                if (!_active)
                    return;

                _stopping = true;
                _active = false;

                _connection.Close();
                _subscriptionsChannel.Close();
            }

            foreach (var workItem in _inProgress.Values)
            {
                workItem.Operation.Fail(
                    new ConnectionClosingException("Work item was still in progress at the moment of manual connection closing."));
            }
            _inProgress.Clear();

            _log.Info("EventStoreConnection closed.");
        }

        public void CreateStream(string stream, Guid id, bool isJson, byte[] metadata)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.NotEmptyGuid(id, "id");
            EnsureActive();

            CreateStreamAsync(stream, id, isJson, metadata).Wait();
        }

        public Task CreateStreamAsync(string stream, Guid id, bool isJson, byte[] metadata)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.NotEmptyGuid(id, "id");
            EnsureActive();

            var source = new TaskCompletionSource<object>();
            var operation = new CreateStreamOperation(source, Guid.NewGuid(), _settings.AllowForwarding, stream, id, isJson, metadata);

            EnqueueOperation(operation);
            return source.Task;
        }

        public void DeleteStream(string stream, int expectedVersion)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            EnsureActive();

            DeleteStreamAsync(stream, expectedVersion).Wait();
        }

        public Task DeleteStreamAsync(string stream, int expectedVersion)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            EnsureActive();

            var source = new TaskCompletionSource<object>();
            var operation = new DeleteStreamOperation(source, Guid.NewGuid(), _settings.AllowForwarding, stream, expectedVersion);

            EnqueueOperation(operation);
            return source.Task;
        }

        public void AppendToStream(string stream, int expectedVersion, IEnumerable<IEvent> events)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.NotNull(events, "events");
            EnsureActive();

            AppendToStreamAsync(stream, expectedVersion, events).Wait();
        }

        public Task AppendToStreamAsync(string stream, int expectedVersion, IEnumerable<IEvent> events)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.NotNull(events, "events");
            EnsureActive();

            var source = new TaskCompletionSource<object>();
            var operation = new AppendToStreamOperation(source, Guid.NewGuid(), _settings.AllowForwarding, stream, expectedVersion, events);

            EnqueueOperation(operation);
            return source.Task;
        }

        public EventStoreTransaction StartTransaction(string stream, int expectedVersion)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            EnsureActive();

            return StartTransactionAsync(stream, expectedVersion).Result;
        }

        public Task<EventStoreTransaction> StartTransactionAsync(string stream, int expectedVersion)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            EnsureActive();

            var source = new TaskCompletionSource<EventStoreTransaction>();
            var operation = new StartTransactionOperation(source, Guid.NewGuid(), _settings.AllowForwarding, stream, expectedVersion);

            EnqueueOperation(operation);
            return source.Task;
        }

        public void TransactionalWrite(long transactionId, string stream, IEnumerable<IEvent> events)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.NotNull(events, "events");
            EnsureActive();

            TransactionalWriteAsync(transactionId, stream, events).Wait();
        }

        public Task TransactionalWriteAsync(long transactionId, string stream, IEnumerable<IEvent> events)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.NotNull(events, "events");
            EnsureActive();

            var source = new TaskCompletionSource<object>();
            var operation = new TransactionalWriteOperation(source, Guid.NewGuid(), _settings.AllowForwarding, transactionId, stream, events);

            EnqueueOperation(operation);
            return source.Task;
        }

        public void CommitTransaction(long transactionId, string stream)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            EnsureActive();

            CommitTransactionAsync(transactionId, stream).Wait();
        }

        public Task CommitTransactionAsync(long transactionId, string stream)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            EnsureActive();

            var source = new TaskCompletionSource<object>();
            var operation = new CommitTransactionOperation(source, Guid.NewGuid(), _settings.AllowForwarding, transactionId, stream);

            EnqueueOperation(operation);
            return source.Task;
        }

        public EventStreamSlice ReadEventStreamForward(string stream, int start, int count)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.Nonnegative(start, "start");
            Ensure.Positive(count, "count");
            EnsureActive();

            return ReadEventStreamForwardAsync(stream, start, count).Result;
        }

        public Task<EventStreamSlice> ReadEventStreamForwardAsync(string stream, int start, int count)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.Nonnegative(start, "start");
            Ensure.Positive(count, "count");
            EnsureActive();

            var source = new TaskCompletionSource<EventStreamSlice>();
            var operation = new ReadStreamEventsForwardOperation(source, Guid.NewGuid(), stream, start, count, true);

            EnqueueOperation(operation);
            return source.Task;
        }

        public EventStreamSlice ReadEventStreamBackward(string stream, int start, int count)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.Positive(count, "count");
            EnsureActive();

            return ReadEventStreamBackwardAsync(stream, start, count).Result;
        }

        public Task<EventStreamSlice> ReadEventStreamBackwardAsync(string stream, int start, int count)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.Positive(count, "count");
            EnsureActive();

            var source = new TaskCompletionSource<EventStreamSlice>();
            var operation = new ReadStreamEventsBackwardOperation(source, Guid.NewGuid(), stream, start, count, true);

            EnqueueOperation(operation);
            return source.Task;
        }

        public AllEventsSlice ReadAllEventsForward(Position position, int maxCount)
        {
            Ensure.NotNull(position, "position");
            Ensure.Positive(maxCount, "maxCount");
            EnsureActive();

            return ReadAllEventsForwardAsync(position, maxCount).Result;
        }

        public Task<AllEventsSlice> ReadAllEventsForwardAsync(Position position, int maxCount)
        {
            Ensure.NotNull(position, "position");
            Ensure.Positive(maxCount, "maxCount");
            EnsureActive();

            var source = new TaskCompletionSource<AllEventsSlice>();
            var operation = new ReadAllEventsForwardOperation(source, Guid.NewGuid(), position, maxCount, true);

            EnqueueOperation(operation);
            return source.Task;
        }

        public AllEventsSlice ReadAllEventsBackward(Position position, int maxCount)
        {
            Ensure.NotNull(position, "position");
            Ensure.Positive(maxCount, "maxCount");
            EnsureActive();

            return ReadAllEventsBackwardAsync(position, maxCount).Result;
        }

        public Task<AllEventsSlice> ReadAllEventsBackwardAsync(Position position, int maxCount)
        {
            Ensure.NotNull(position, "position");
            Ensure.Positive(maxCount, "maxCount");
            EnsureActive();

            var source = new TaskCompletionSource<AllEventsSlice>();
            var operation = new ReadAllEventsBackwardOperation(source, Guid.NewGuid(), position, maxCount, true);

            EnqueueOperation(operation);
            return source.Task;
        }

        private void EnqueueOperation(IClientOperation operation)
        {
            while (_queue.Count >= _settings.MaxQueueSize)
            {
                Thread.Sleep(1);
            }
            _queue.Enqueue(operation);
        }

        public Task SubscribeAsync(string stream, Action<RecordedEvent, Position> eventAppeared, Action subscriptionDropped)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.NotNull(eventAppeared, "eventAppeared");
            Ensure.NotNull(subscriptionDropped, "subscriptionDropped");
            EnsureActive();

            _subscriptionsChannel.EnsureConnected(_tcpEndPoint);
            return _subscriptionsChannel.Subscribe(stream, eventAppeared, subscriptionDropped);
        }

        public Task UnsubscribeAsync(string stream)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            EnsureActive();

            _subscriptionsChannel.EnsureConnected(_tcpEndPoint);
            _subscriptionsChannel.Unsubscribe(stream);
            return Tasks.CreateCompleted();
        }

        public Task SubscribeToAllStreamsAsync(Action<RecordedEvent, Position> eventAppeared, Action subscriptionDropped)
        {
            Ensure.NotNull(eventAppeared, "eventAppeared");
            Ensure.NotNull(subscriptionDropped, "subscriptionDropped");
            EnsureActive();

            _subscriptionsChannel.EnsureConnected(_tcpEndPoint);
            return _subscriptionsChannel.SubscribeToAllStreams(eventAppeared, subscriptionDropped);
        }

        public Task UnsubscribeFromAllStreamsAsync()
        {
            EnsureActive();

            _subscriptionsChannel.EnsureConnected(_tcpEndPoint);
            _subscriptionsChannel.UnsubscribeFromAllStreams();
            return Tasks.CreateCompleted();
        }

        private void MainLoop()
        {
            while (!_stopping)
            {
                IClientOperation operation;
                if (_inProgressCount < _settings.MaxConcurrentItems && _queue.TryDequeue(out operation))
                {
                    Interlocked.Increment(ref _inProgressCount);
                    Send(new WorkItem(operation));
                }
                else
                {
                    Thread.Sleep(1);
                }

                lock (_connectionLock)
                {
                    if (_reconnectionStopwatch.IsRunning && _reconnectionStopwatch.Elapsed >= _settings.ReconnectionDelay)
                    {
                        _reconnectionsCount += 1;
                        if (_reconnectionsCount > _settings.MaxReconnections)
                            Close();

                        _lastReconnectionTimestamp = DateTime.UtcNow;
                        _connection = _connector.CreateTcpConnection(_tcpEndPoint, OnPackageReceived, OnConnectionEstablished, OnConnectionClosed);
                        _reconnectionStopwatch.Stop();
                    }
                }

                if (_timeoutCheckStopwatch.Elapsed > _settings.OperationTimeoutCheckPeriod)
                {
                    var now = DateTime.UtcNow;
                    var retriable = new List<WorkItem>();
                    foreach (var workerItem in _inProgress.Values)
                    {
                        var lastUpdated = new DateTime(Interlocked.Read(ref workerItem.LastUpdatedTicks));
                        if (now - lastUpdated > _settings.OperationTimeout)
                        {
                            if (lastUpdated >= _lastReconnectionTimestamp)
                            {
                                var err = string.Format("{0} never got response from server" +
                                                        "Last state update : {1}, last reconnect : {2}, now(utc) : {3}.",
                                                        workerItem,
                                                        lastUpdated,
                                                        _lastReconnectionTimestamp,
                                                        now);
                                if (TryRemoveWorkItem(workerItem))
                                {
                                    _log.Error(err);
                                    workerItem.Operation.Fail(new OperationTimedOutException(err));
                                }
                                _log.Error(err);
                            }
                            else
                            {
                                retriable.Add(workerItem);
                            }
                        }
                    }

                    foreach (var workItem in retriable.OrderBy(wi => wi.SeqNo))
                    {
                        Retry(workItem);
                    }

                    _timeoutCheckStopwatch.Restart();
                }
            }
        }

        private bool TryRemoveWorkItem(WorkItem workItem)
        {
            WorkItem removed;
            if (!_inProgress.TryRemove(workItem.Operation.CorrelationId, out removed))
                return false;

            Interlocked.Decrement(ref _inProgressCount);
            return true;
        }

        private void Send(WorkItem workItem)
        {
            lock (_connectionLock)
            {
                _inProgress.TryAdd(workItem.Operation.CorrelationId, workItem);
                _connection.EnqueueSend(workItem.Operation.CreateNetworkPackage().AsByteArray());
            }
        }

        private void Retry(WorkItem workItem)
        {
            lock (_connectionLock)
            {
                WorkItem inProgressItem;
                if (_inProgress.TryRemove(workItem.Operation.CorrelationId, out inProgressItem))
                {
                    inProgressItem.Attempt += 1;
                    if (inProgressItem.Attempt > _settings.MaxAttempts)
                    {
                        _log.Error("Retries limit reached for : {0}", inProgressItem);
                        inProgressItem.Operation.Fail(new RetriesLimitReachedException(inProgressItem.ToString(),
                                                                                       inProgressItem.Attempt));
                    }
                    else
                    {
                        inProgressItem.Operation.SetRetryId(Guid.NewGuid());
                        Interlocked.Exchange(ref inProgressItem.LastUpdatedTicks, DateTime.UtcNow.Ticks);
                        Send(inProgressItem);
                    }
                }
                else
                {
                    _log.Error("Concurrency failure. Unable to remove in progress item on retry");
                }
            }
        }

        private void Reconnect(WorkItem workItem, IPEndPoint tcpEndpoint)
        {
            lock (_connectionLock)
            {
                if (!_reconnectionStopwatch.IsRunning || (_reconnectionStopwatch.IsRunning && !_tcpEndPoint.Equals(tcpEndpoint)))
                {
                    _log.Info("Going to reconnect to [{0}]. Current state: {1}, Current endpoint: {2}",
                              tcpEndpoint,
                              _reconnectionStopwatch.IsRunning ? "reconnecting" : "connected",
                              _tcpEndPoint);

                    _tcpEndPoint = tcpEndpoint;
                    _connection.Close();
                    _subscriptionsChannel.Close(false);
                }
                Retry(workItem);
            }
        }

        private void OnPackageReceived(TcpTypedConnection connection, TcpPackage package)
        {
            var corrId = package.CorrelationId;
            WorkItem workItem;

            if (!_inProgress.TryGetValue(corrId, out workItem))
            {
                _log.Error("Unexpected CorrelationId received {{{0}}}", corrId);
                return;
            }

            var result = workItem.Operation.InspectPackage(package);
            switch (result.Decision)
            {
                case InspectionDecision.Succeed:
                {
                    if (TryRemoveWorkItem(workItem))
                        workItem.Operation.Complete();
                    break;
                }
                case InspectionDecision.Retry:
                {
                    Retry(workItem);
                    break;
                }
                case InspectionDecision.Reconnect:
                {
                    Reconnect(workItem, (IPEndPoint) result.Data);
                    break;
                }
                case InspectionDecision.NotifyError:
                {
                    if (TryRemoveWorkItem(workItem))
                        workItem.Operation.Fail(result.Error);
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void OnConnectionEstablished(TcpTypedConnection tcpTypedConnection)
        {
            lock (_connectionLock)
            {
                _reconnectionsCount = 0;
            }
        }

        private void OnConnectionClosed(TcpTypedConnection connection, IPEndPoint endPoint, SocketError error)
        {
            lock (_connectionLock)
            {
                _reconnectionStopwatch.Restart();
            }
        }
    }
}