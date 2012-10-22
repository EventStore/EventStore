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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI.ClientOperations;
using EventStore.ClientAPI.Common.Log;
using EventStore.ClientAPI.Connection;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.Transport.Tcp;
using Ensure = EventStore.ClientAPI.Common.Utils.Ensure;
using System.Linq;

namespace EventStore.ClientAPI
{
    public class EventStoreConnection : IProjectionsManagement, IDisposable
    {
        private readonly ILogger _log;

        private const int MaxQueueSize = 5000;

        private readonly int _maxConcurrentItems;
        private readonly int _maxAttempts;
        private readonly int _maxReconnections;

        private static readonly TimeSpan ReconnectionDelay = TimeSpan.FromSeconds(0.5);
        private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(7);
        private static readonly TimeSpan OperationTimeoutCheckPeriod = TimeSpan.FromSeconds(1);

        private readonly IPEndPoint _tcpEndPoint;

        private readonly TcpConnector _connector;
        private TcpTypedConnection _connection;
        private readonly object _connectionLock = new object();

        private readonly SubscriptionsChannel _subscriptionsChannel;

        private readonly ProjectionsManager _projectionsManager;

#if __MonoCS__
        private readonly Common.ConcurrentCollections.ConcurrentQueue<IClientOperation> _queue = new Common.ConcurrentCollections.ConcurrentQueue<IClientOperation>();
#else
        private readonly ConcurrentQueue<IClientOperation> _queue = new ConcurrentQueue<IClientOperation>();
#endif
        private readonly ConcurrentDictionary<Guid, WorkItem> _inProgress = new ConcurrentDictionary<Guid, WorkItem>();
        private int _inProgressCount;

        private DateTime _lastReconnectionTimestamp;
        private readonly Stopwatch _reconnectionStopwatch = new Stopwatch();
        private readonly Stopwatch _timeoutCheckStopwatch = new Stopwatch();
        private int _reconnectionsCount;

        private readonly Thread _worker;
        private volatile bool _stopping;

        public IProjectionsManagement Projections
        {
            get
            {
                return this;
            }
        }

        public EventStoreConnection(IPEndPoint tcpEndPoint, 
                                    int maxConcurrentRequests = 5000,
                                    int maxAttemptsForOperation = 10,
                                    int maxReconnections = 10,
                                    ILogger logger = null)
        {
            Ensure.NotNull(tcpEndPoint, "tcpEndPoint");
            Ensure.Positive(maxConcurrentRequests, "maxConcurrentRequests");
            Ensure.Nonnegative(maxAttemptsForOperation, "maxAttemptsForOperation");
            Ensure.Nonnegative(maxReconnections, "maxReconnections");

            _tcpEndPoint = tcpEndPoint;
            _maxConcurrentItems = maxConcurrentRequests;
            _maxAttempts = maxAttemptsForOperation;
            _maxReconnections = maxReconnections;

            LogManager.RegisterLogger(logger);
            _log = LogManager.GetLogger();

            _connector = new TcpConnector(_tcpEndPoint);
            _subscriptionsChannel = new SubscriptionsChannel(_connector);
            //TODO TD: WAT?
            _projectionsManager = new ProjectionsManager(new IPEndPoint(_tcpEndPoint.Address, _tcpEndPoint.Port + 1000));

            _lastReconnectionTimestamp = DateTime.UtcNow;
            _connection = _connector.CreateTcpConnection(OnPackageReceived, OnConnectionEstablished, OnConnectionClosed);
            _timeoutCheckStopwatch.Start();

            _worker = new Thread(MainLoop)
            {
                IsBackground = true,
                Name = "Worker thread"
            };
            _worker.Start();
        }

        public void Close()
        {
            _stopping = true;

            lock (_connectionLock)
            {
                _connection.Close();
            }
            _subscriptionsChannel.Close();

            var items = _inProgress.Values;
            _inProgress.Clear();
            foreach (var workItem in items)
            {
                workItem.Operation.Fail(new ConnectionClosingException(
                    "Work item was still in progress at the moment of manual connection closing"));
            }

            _log.Info("EventStoreConnection closed.");
        }

        void IDisposable.Dispose()
        {
            Close();
        }

        public void CreateStream(string stream, byte[] metadata)
        {
            Ensure.NotNullOrEmpty(stream, "stream");

            var task = CreateStreamAsync(stream, metadata);
            task.Wait();
        }

        public Task CreateStreamAsync(string stream, byte[] metadata)
        {
            Ensure.NotNullOrEmpty(stream, "stream");

            var source = new TaskCompletionSource<object>();
            var operation = new CreateStreamOperation(source, Guid.NewGuid(), stream, metadata);

            EnqueueOperation(operation);
            return source.Task;
        }

        public void DeleteStream(string stream, int expectedVersion)
        {
            Ensure.NotNullOrEmpty(stream, "stream");

            var task = DeleteStreamAsync(stream, expectedVersion);
            task.Wait();
        }

        public Task DeleteStreamAsync(string stream, int expectedVersion)
        {
            Ensure.NotNullOrEmpty(stream, "stream");

            var source = new TaskCompletionSource<object>();
            var operation = new DeleteStreamOperation(source, Guid.NewGuid(), stream, expectedVersion);

            EnqueueOperation(operation);
            return source.Task;
        }

        public void AppendToStream(string stream, int expectedVersion, IEnumerable<IEvent> events)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.NotNull(events, "events");

            var task = AppendToStreamAsync(stream, expectedVersion, events);
            task.Wait();
        }

        public Task AppendToStreamAsync(string stream, int expectedVersion, IEnumerable<IEvent> events)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.NotNull(events, "events");

            var source = new TaskCompletionSource<object>();
            var operation = new AppendToStreamOperation(source, Guid.NewGuid(), stream, expectedVersion, events);

            EnqueueOperation(operation);
            return source.Task;
        }

        public EventStoreTransaction StartTransaction(string stream, int expectedVersion)
        {
            Ensure.NotNullOrEmpty(stream, "stream");

            var task = StartTransactionAsync(stream, expectedVersion);
            task.Wait();
            return task.Result;
        }

        public Task<EventStoreTransaction> StartTransactionAsync(string stream, int expectedVersion)
        {
            Ensure.NotNullOrEmpty(stream, "stream");

            var source = new TaskCompletionSource<EventStoreTransaction>();
            var operation = new StartTransactionOperation(source, Guid.NewGuid(), stream, expectedVersion);

            EnqueueOperation(operation);
            return source.Task;
        }

        public void TransactionalWrite(long transactionId, string stream, IEnumerable<IEvent> events)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.NotNull(events, "events");

            var task = TransactionalWriteAsync(transactionId, stream, events);
            task.Wait();
        }

        public Task TransactionalWriteAsync(long transactionId, string stream, IEnumerable<IEvent> events)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.NotNull(events, "events");

            var source = new TaskCompletionSource<object>();
            var operation = new TransactionalWriteOperation(source, Guid.NewGuid(), transactionId, stream, events);

            EnqueueOperation(operation);
            return source.Task;
        }

        public void CommitTransaction(long transactionId, string stream)
        {
            Ensure.NotNullOrEmpty(stream, "stream");

            var task = CommitTransactionAsync(transactionId, stream);
            task.Wait();
        }

        public Task CommitTransactionAsync(long transactionId, string stream)
        {
            Ensure.NotNullOrEmpty(stream, "stream");

            var source = new TaskCompletionSource<object>();
            var operation = new CommitTransactionOperation(source, Guid.NewGuid(), transactionId, stream);

            EnqueueOperation(operation);
            return source.Task;
        }

        public EventStreamSlice ReadEventStreamForward(string stream, int start, int count)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.Nonnegative(start, "start");
            Ensure.Positive(count, "count");

            var task = ReadEventStreamForwardAsync(stream, start, count);
            task.Wait();

            return task.Result;
        }

        public Task<EventStreamSlice> ReadEventStreamForwardAsync(string stream, int start, int count)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.Nonnegative(start, "start");
            Ensure.Positive(count, "count");

            var source = new TaskCompletionSource<EventStreamSlice>();
            var operation = new ReadStreamEventsForwardOperation(source, Guid.NewGuid(), stream, start, count, true);

            EnqueueOperation(operation);
            return source.Task;
        }

        public EventStreamSlice ReadEventStreamBackward(string stream, int start, int count)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.Positive(count, "count");

            var task = ReadEventStreamBackwardAsync(stream, start, count);
            task.Wait();

            return task.Result;
        }

        public Task<EventStreamSlice> ReadEventStreamBackwardAsync(string stream, int start, int count)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.Positive(count, "count");

            var source = new TaskCompletionSource<EventStreamSlice>();
            var operation = new ReadStreamEventsBackwardOperation(source, Guid.NewGuid(), stream, start, count, true);

            EnqueueOperation(operation);
            return source.Task;
        }

        public AllEventsSlice ReadAllEventsForward(Position position, int maxCount)
        {
            Ensure.NotNull(position, "position");
            Ensure.Positive(maxCount, "maxCount");

            var task = ReadAllEventsForwardAsync(position, maxCount);
            task.Wait();
            return task.Result;
        }

        public Task<AllEventsSlice> ReadAllEventsForwardAsync(Position position, int maxCount)
        {
            Ensure.NotNull(position, "position");
            Ensure.Positive(maxCount, "maxCount");

            var source = new TaskCompletionSource<AllEventsSlice>();
            var operation = new ReadAllEventsForwardOperation(source, Guid.NewGuid(), position, maxCount, true);

            EnqueueOperation(operation);
            return source.Task;
        }

        public AllEventsSlice ReadAllEventsBackward(Position position, int maxCount)
        {
            Ensure.NotNull(position, "position");
            Ensure.Positive(maxCount, "maxCount");

            var task = ReadAllEventsBackwardAsync(position, maxCount);
            task.Wait();
            return task.Result;
        }

        public Task<AllEventsSlice> ReadAllEventsBackwardAsync(Position position, int maxCount)
        {
            Ensure.NotNull(position, "position");
            Ensure.Positive(maxCount, "maxCount");

            var source = new TaskCompletionSource<AllEventsSlice>();
            var operation = new ReadAllEventsBackwardOperation(source, Guid.NewGuid(), position, maxCount, true);

            EnqueueOperation(operation);
            return source.Task;
        }

        public Task SubscribeAsync(string stream, Action<RecordedEvent> eventAppeared, Action subscriptionDropped)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.NotNull(eventAppeared, "eventAppeared");
            Ensure.NotNull(subscriptionDropped, "subscriptionDropped");

            _subscriptionsChannel.EnsureConnected();
            return _subscriptionsChannel.Subscribe(stream, eventAppeared, subscriptionDropped);
        }

        public void Unsubscribe(string stream)
        {
            Ensure.NotNullOrEmpty(stream, "stream");

            _subscriptionsChannel.EnsureConnected();
            _subscriptionsChannel.Unsubscribe(stream);
        }

        public Task SubscribeToAllStreamsAsync(Action<RecordedEvent> eventAppeared, Action subscriptionDropped)
        {
            Ensure.NotNull(eventAppeared, "eventAppeared");
            Ensure.NotNull(subscriptionDropped, "subscriptionDropped");

            _subscriptionsChannel.EnsureConnected();
            return _subscriptionsChannel.SubscribeToAllStreams(eventAppeared, subscriptionDropped);
        }

        public void UnsubscribeFromAllStreams()
        {
            _subscriptionsChannel.EnsureConnected();
            _subscriptionsChannel.UnsubscribeFromAllStreams();
        }

        void IProjectionsManagement.Enable(string name)
        {
            Ensure.NotNullOrEmpty(name, "name");

            var task = ((IProjectionsManagement) this).EnableAsync(name);
            task.Wait();
        }

        Task IProjectionsManagement.EnableAsync(string name)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return _projectionsManager.Enable(name);
        }

        void IProjectionsManagement.Disable(string name)
        {
            Ensure.NotNullOrEmpty(name, "name");

            var task = ((IProjectionsManagement) this).DisableAsync(name);
            task.Wait();
        }

        Task IProjectionsManagement.DisableAsync(string name)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return _projectionsManager.Disable(name);
        }

        void IProjectionsManagement.CreateOneTime(string query)
        {
            Ensure.NotNullOrEmpty(query, "query");

            var task = ((IProjectionsManagement) this).CreateOneTimeAsync(query);
            task.Wait();
        }

        Task IProjectionsManagement.CreateOneTimeAsync(string query)
        {
            Ensure.NotNullOrEmpty(query, "query");
            return _projectionsManager.CreateOneTime(query);
        }

        void IProjectionsManagement.CreateAdHoc(string name, string query)
        {
            Ensure.NotNullOrEmpty(name, "name");
            Ensure.NotNullOrEmpty(query, "query");

            var task = ((IProjectionsManagement) this).CreateAdHocAsync(name, query);
            task.Wait();
        }

        Task IProjectionsManagement.CreateAdHocAsync(string name, string query)
        {
            Ensure.NotNullOrEmpty(name, "name");
            Ensure.NotNullOrEmpty(query, "query");

            return _projectionsManager.CreateAdHoc(name, query);
        }

        void IProjectionsManagement.CreateContinuous(string name, string query)
        {
            Ensure.NotNullOrEmpty(name, "name");
            Ensure.NotNullOrEmpty(query, "query");

            var task = ((IProjectionsManagement) this).CreateContinuousAsync(name, query);
            task.Wait();
        }

        Task IProjectionsManagement.CreateContinuousAsync(string name, string query)
        {
            Ensure.NotNullOrEmpty(name, "name");
            Ensure.NotNullOrEmpty(query, "query");

            return _projectionsManager.CreateContinious(name, query);
        }

        void IProjectionsManagement.CreatePersistent(string name, string query)
        {
            Ensure.NotNullOrEmpty(name, "name");
            Ensure.NotNullOrEmpty(query, "query");

            var task = ((IProjectionsManagement) this).CreatePersistentAsync(name, query);
            task.Wait();
        }

        Task IProjectionsManagement.CreatePersistentAsync(string name, string query)
        {
            Ensure.NotNullOrEmpty(name, "name");
            Ensure.NotNullOrEmpty(query, "query");

            return _projectionsManager.CreatePersistent(name, query);
        }

        string IProjectionsManagement.ListAll()
        {
            var task = ((IProjectionsManagement) this).ListAllAsync();
            task.Wait();
            return task.Result;
        }

        Task<string> IProjectionsManagement.ListAllAsync()
        {
            return _projectionsManager.ListAll();
        }

        string IProjectionsManagement.ListOneTime()
        {
            var task = ((IProjectionsManagement) this).ListOneTimeAsync();
            task.Wait();
            return task.Result;
        }

        Task<string> IProjectionsManagement.ListOneTimeAsync()
        {
            return _projectionsManager.ListOneTime();
        }

        string IProjectionsManagement.ListAdHoc()
        {
            var task = ((IProjectionsManagement) this).ListAdHocAsync();
            task.Wait();
            return task.Result;
        }

        Task<string> IProjectionsManagement.ListAdHocAsync()
        {
            return _projectionsManager.ListAdHoc();
        }

        string IProjectionsManagement.ListContinuous()
        {
            var task = ((IProjectionsManagement) this).ListContinuousAsync();
            task.Wait();
            return task.Result;
        }

        Task<string> IProjectionsManagement.ListContinuousAsync()
        {
            return _projectionsManager.ListContinuous();
        }

        string IProjectionsManagement.ListPersistent()
        {
            var task = ((IProjectionsManagement) this).ListPersistentAsync();
            task.Wait();
            return task.Result;
        }

        Task<string> IProjectionsManagement.ListPersistentAsync()
        {
            return _projectionsManager.ListPersistent();
        }

        string IProjectionsManagement.GetStatus(string name)
        {
            Ensure.NotNullOrEmpty(name, "name");

            var task = ((IProjectionsManagement) this).GetStatusAsync(name);
            task.Wait();
            return task.Result;
        }

        Task<string> IProjectionsManagement.GetStatusAsync(string name)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return _projectionsManager.GetStatus(name);
        }

        string IProjectionsManagement.GetState(string name)
        {
            Ensure.NotNullOrEmpty(name, "name");

            var task = ((IProjectionsManagement) this).GetStateAsync(name);
            task.Wait();
            return task.Result;
        }

        Task<string> IProjectionsManagement.GetStateAsync(string name)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return _projectionsManager.GetState(name);
        }

        string IProjectionsManagement.GetStatistics(string name)
        {
            Ensure.NotNullOrEmpty(name, "name");

            var task = ((IProjectionsManagement) this).GetStatisticsAsync(name);
            task.Wait();
            return task.Result;
        }

        Task<string> IProjectionsManagement.GetStatisticsAsync(string name)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return _projectionsManager.GetStatistics(name);
        }

        string IProjectionsManagement.GetQuery(string name)
        {
            Ensure.NotNullOrEmpty(name, "name");

            var task = ((IProjectionsManagement) this).GetQueryAsync(name);
            task.Wait();
            return task.Result;
        }

        Task<string> IProjectionsManagement.GetQueryAsync(string name)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return _projectionsManager.GetQuery(name);
        }

        void IProjectionsManagement.UpdateQuery(string name, string query)
        {
            Ensure.NotNullOrEmpty(name, "name");
            Ensure.NotNullOrEmpty(query, "query");

            var task = ((IProjectionsManagement) this).UpdateQueryAsync(name, query);
            task.Wait();
        }

        Task IProjectionsManagement.UpdateQueryAsync(string name, string query)
        {
            Ensure.NotNullOrEmpty(name, "name");
            Ensure.NotNullOrEmpty(query, "query");

            return _projectionsManager.UpdateQuery(name, query);
        }

        void IProjectionsManagement.Delete(string name)
        {
            Ensure.NotNullOrEmpty(name, "name");

            var task = ((IProjectionsManagement) this).DeleteAsync(name);
            task.Wait();
        }

        Task IProjectionsManagement.DeleteAsync(string name)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return _projectionsManager.Delete(name);
        }

        private void EnqueueOperation(IClientOperation operation)
        {
            while (_queue.Count >= MaxQueueSize)
            {
                Thread.Sleep(1);
            }
            _queue.Enqueue(operation);
        }

        private void MainLoop()
        {
            while (!_stopping)
            {
                IClientOperation operation;
                if (_inProgressCount < _maxConcurrentItems && _queue.TryDequeue(out operation))
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
                    if (_reconnectionStopwatch.IsRunning && _reconnectionStopwatch.Elapsed >= ReconnectionDelay)
                    {
                        _reconnectionsCount += 1;
                        if (_reconnectionsCount > _maxReconnections)
                            throw new CannotEstablishConnectionException();

                        _lastReconnectionTimestamp = DateTime.UtcNow;
                        _connection = _connector.CreateTcpConnection(OnPackageReceived, OnConnectionEstablished, OnConnectionClosed);
                        _reconnectionStopwatch.Stop();
                    }
                }

                if (_timeoutCheckStopwatch.Elapsed > OperationTimeoutCheckPeriod)
                {
                    var now = DateTime.UtcNow;
                    var retriable = new List<WorkItem>();
                    foreach (var workerItem in _inProgress.Values)
                    {
                        var lastUpdated = new DateTime(Interlocked.Read(ref workerItem.LastUpdatedTicks));
                        if (now - lastUpdated > OperationTimeout)
                        {
                            if (lastUpdated >= _lastReconnectionTimestamp)
                            {
                                var err = string.Format("{0} never got response from server" +
                                                        "Last state update : {1}, last reconnect : {2}, now(utc) : {3}.",
                                                        workerItem,
                                                        lastUpdated,
                                                        _lastReconnectionTimestamp,
                                                        now);
//                                if (TryRemoveWorkItem(workerItem))
//                                {
//                                    _log.Error(err);
//                                    workerItem.Operation.Fail(new OperationTimedOutException(err));
//                                }
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
                    if (inProgressItem.Attempt > _maxAttempts)
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

        private void OnPackageReceived(TcpTypedConnection connection, TcpPackage package)
        {
            var corrId = package.CorrelationId;
            WorkItem workItem;

            if (!_inProgress.TryGetValue(corrId, out workItem))
            {
                _log.Error("Unexpected corrid received {0}", corrId);
                return;
            }

            var result = workItem.Operation.InspectPackage(package);
            switch (result.Decision)
            {
                case InspectionDecision.Succeed:
                    if (TryRemoveWorkItem(workItem))
                        workItem.Operation.Complete();
                    break;
                case InspectionDecision.Retry:
                    Retry(workItem);
                    break;
                case InspectionDecision.NotifyError:
                    if (TryRemoveWorkItem(workItem))
                        workItem.Operation.Fail(result.Error);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void OnConnectionEstablished(TcpTypedConnection tcpTypedConnection)
        {
            lock(_connectionLock)
                _reconnectionsCount = 0;
        }

        private void OnConnectionClosed(TcpTypedConnection connection, IPEndPoint endPoint, SocketError error)
        {
            lock (_connectionLock)
                _reconnectionStopwatch.Restart();
        }
    }
}