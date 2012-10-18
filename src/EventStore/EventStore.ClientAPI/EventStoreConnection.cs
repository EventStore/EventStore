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
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.System;
using EventStore.ClientAPI.Transport.Http;
using EventStore.ClientAPI.Transport.Tcp;
using Connection = EventStore.ClientAPI.Transport.Tcp.TcpTypedConnection;
using Ensure = EventStore.ClientAPI.Common.Utils.Ensure;
using System.Linq;
using HttpStatusCode = EventStore.ClientAPI.Transport.Http.HttpStatusCode;

namespace EventStore.ClientAPI
{
    internal class WorkItem
    {
        private static long _seqNumber = -1;

        public readonly long SeqNo;
        public IClientOperation Operation;

        public int Attempt;
        public long LastUpdatedTicks;

        public WorkItem(IClientOperation operation)
        {
            Ensure.NotNull(operation, "operation");
            SeqNo = NextSeqNo();
            Operation = operation;

            Attempt = 0;
            LastUpdatedTicks = DateTime.UtcNow.Ticks;
        }

        private static long NextSeqNo()
        {
            return Interlocked.Increment(ref _seqNumber);
        }

        public override string ToString()
        {
            return string.Format("Workitem {0}: {1}, attempt {2}, seqNo {3}", 
                                 Operation.GetType().FullName, 
                                 Operation,
                                 Attempt, 
                                 SeqNo);
        }
    }

    public class EventStoreConnection : IProjectionsManagement,
                                        IDisposable
    {
        private const int MaxQueueSize = 5000;

        private readonly int _maxConcurrentItems;
        private readonly int _maxAttempts;
        private readonly int _maxReconnections;

        private static readonly TimeSpan ReconnectionDelay = TimeSpan.FromSeconds(0.5);
        private static readonly TimeSpan EventTimeoutDelay = TimeSpan.FromSeconds(7);
        private static readonly TimeSpan EventTimeoutCheckPeriod = TimeSpan.FromSeconds(1);

        private readonly IPEndPoint _tcpEndPoint;

        private readonly TcpConnector _connector;
        private Connection _connection;
        private readonly object _connectionLock = new object();

        private readonly SubscriptionsChannel _subscriptionsChannel;
        private readonly object _subscriptionChannelLock = new object();

        private readonly ProjectionsManager _projectionsManager;

        private readonly ConcurrentQueue<IClientOperation> _queue = new ConcurrentQueue<IClientOperation>();
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
                                    int maxReconnections = 10)
        {
            Ensure.NotNull(tcpEndPoint, "tcpEndPoint");
            Ensure.Positive(maxConcurrentRequests, "maxConcurrentRequests");
            Ensure.Nonnegative(maxAttemptsForOperation, "maxAttemptsForOperation");
            Ensure.Nonnegative(maxReconnections, "maxReconnections");

            _tcpEndPoint = tcpEndPoint;
            _maxConcurrentItems = maxConcurrentRequests;
            _maxAttempts = maxAttemptsForOperation;
            _maxReconnections = maxReconnections;

            _connector = new TcpConnector(_tcpEndPoint);
            _subscriptionsChannel = new SubscriptionsChannel(_tcpEndPoint);
            _projectionsManager = new ProjectionsManager(new IPEndPoint(_tcpEndPoint.Address, _tcpEndPoint.Port + 1000));

            _lastReconnectionTimestamp = DateTime.UtcNow;
            _connection = _connector.CreateTcpConnection(OnPackageReceived, OnConnectionEstablished, OnConnectionClosed);
            _timeoutCheckStopwatch.Start();

            _worker = new Thread(MainLoop)
            {
                IsBackground = true,
                Name = string.Format("Worker thread")
            };
            _worker.Start();
        }

        public void Close()
        {
            _stopping = true;

            _connection.Close();
            _subscriptionsChannel.Close();

            const string err = "Work item was still in progress at the moment of manual connection closing";
            var items = _inProgress.Values;
            _inProgress.Clear();
            foreach(var workItem in items)
                workItem.Operation.Fail(new ConnectionClosingException(err));
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

            EnsureSubscriptionChannelConnected();
            return _subscriptionsChannel.Subscribe(stream, eventAppeared, subscriptionDropped);
        }

        public void Unsubscribe(string stream)
        {
            Ensure.NotNullOrEmpty(stream, "stream");

            EnsureSubscriptionChannelConnected();
            _subscriptionsChannel.Unsubscribe(stream);
        }

        public Task SubscribeToAllStreamsAsync(Action<RecordedEvent> eventAppeared, Action subscriptionDropped)
        {
            Ensure.NotNull(eventAppeared, "eventAppeared");
            Ensure.NotNull(subscriptionDropped, "subscriptionDropped");

            EnsureSubscriptionChannelConnected();
            return _subscriptionsChannel.SubscribeToAllStreams(eventAppeared, subscriptionDropped);
        }

        public void UnsubscribeFromAllStreams()
        {
            EnsureSubscriptionChannelConnected();
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
                Thread.Sleep(1);
            
            _queue.Enqueue(operation);
        }

        private void EnsureSubscriptionChannelConnected()
        {
            if (!_subscriptionsChannel.ConnectedEvent.WaitOne(0))
            {
                lock (_subscriptionChannelLock)
                {
                    if (!_subscriptionsChannel.ConnectedEvent.WaitOne(0))
                    {
                        _subscriptionsChannel.Connect();
                        if (!_subscriptionsChannel.ConnectedEvent.WaitOne(500))
                            throw new CannotEstablishConnectionException(string.Format("Cannot connect to {0}", _tcpEndPoint));
                    }
                }
            }
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
                    Thread.Sleep(1);

                lock (_connectionLock)
                {
                    if (_reconnectionStopwatch.IsRunning && _reconnectionStopwatch.Elapsed >= ReconnectionDelay)
                    {
                        _reconnectionsCount += 1;
                        if(_reconnectionsCount > _maxReconnections)
                            throw new CannotEstablishConnectionException();

                        _lastReconnectionTimestamp = DateTime.UtcNow;
                        _connection = _connector.CreateTcpConnection(OnPackageReceived, OnConnectionEstablished, OnConnectionClosed);
                        _reconnectionStopwatch.Stop();
                    }
                }

                if (_timeoutCheckStopwatch.Elapsed > EventTimeoutCheckPeriod)
                {
                    var now = DateTime.UtcNow;
                    var retriable = new List<WorkItem>();
                    foreach (var workerItem in _inProgress.Values)
                    {
                        var lastUpdated = new DateTime(Interlocked.Read(ref workerItem.LastUpdatedTicks));
                        if (now - lastUpdated > EventTimeoutDelay)
                        {
                            if (lastUpdated > _lastReconnectionTimestamp)
                            {
                                var err = string.Format("{0} never got response from server"+ 
                                                        "Last state update : {1}, last reconnect : {2}, now(utc) : {3}.",
                                                        workerItem,
                                                        lastUpdated,
                                                        _lastReconnectionTimestamp,
                                                        now);
                                if(TryRemoveWorkItem(workerItem))
                                    workerItem.Operation.Fail(new OperationTimedOutException(err));
                            }
                            else
                                retriable.Add(workerItem);
                        }
                    }

                    foreach (var workItem in retriable.OrderBy(wi => wi.SeqNo))
                        Retry(workItem);

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
                    inProgressItem.Operation.SetRetryId(Guid.NewGuid());
                    inProgressItem.Attempt += 1;
                    Interlocked.Exchange(ref inProgressItem.LastUpdatedTicks, DateTime.UtcNow.Ticks);

                    if (inProgressItem.Attempt > _maxAttempts)
                        inProgressItem.Operation.Fail(new RetriesLimitReachedException(inProgressItem.Operation.ToString(),
                                                                                       inProgressItem.Attempt));
                    else
                        Send(inProgressItem);
                }
                else
                    Debug.WriteLine("Concurrency failure. Unable to remove in progress item on retry");
            }
        }

        private void OnPackageReceived(Connection connection, TcpPackage package)
        {
            var corrId = package.CorrelationId;
            WorkItem workItem;

            if (!_inProgress.TryGetValue(corrId, out workItem))
            {
                Debug.WriteLine("Unexpected corrid received {0}", corrId);
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
                    if(TryRemoveWorkItem(workItem))
                        workItem.Operation.Fail(result.Error);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void OnConnectionEstablished(Connection tcpTypedConnection)
        {
            lock(_connectionLock)
                _reconnectionsCount = 0;
        }

        private void OnConnectionClosed(Connection connection, IPEndPoint endPoint, SocketError error)
        {
            lock (_connectionLock)
                _reconnectionStopwatch.Restart();
        }
    }

    internal class Subscription
    {
        public readonly TaskCompletionSource<object> Source;

        public readonly Guid Id;
        public readonly string Stream;

        public readonly Action<RecordedEvent> EventAppeared;
        public readonly Action SubscriptionDropped;

        public Subscription(TaskCompletionSource<object> source,
                            Guid id,
                            string stream,
                            Action<RecordedEvent> eventAppeared,
                            Action subscriptionDropped)
        {
            Source = source;

            Id = id;
            Stream = stream;

            EventAppeared = eventAppeared;
            SubscriptionDropped = subscriptionDropped;
        }

        public Subscription(TaskCompletionSource<object> source,
                            Guid id,
                            Action<RecordedEvent> eventAppeared,
                            Action subscriptionDropped)
        {
            Source = source;

            Id = id;
            Stream = null;

            EventAppeared = eventAppeared;
            SubscriptionDropped = subscriptionDropped;
        }
    }

    internal class SubscriptionsChannel
    {
        private readonly IPEndPoint _tcpEndPoint;

        private readonly TcpConnector _connector;
        private Connection _connection;

        internal ManualResetEvent ConnectedEvent = new ManualResetEvent(false);

        private Thread _executionThread;
        private volatile bool _stopExecutionThread;
        private readonly ConcurrentQueue<Action> _executionQueue = new ConcurrentQueue<Action>(); 

        private readonly ConcurrentDictionary<Guid, Subscription> _subscriptions = new ConcurrentDictionary<Guid, Subscription>();

        public SubscriptionsChannel(IPEndPoint tcpEndPoint)
        {
            _tcpEndPoint = tcpEndPoint;
            _connector = new TcpConnector(_tcpEndPoint);
        }

        public void Connect()
        {
            _connection = _connector.CreateTcpConnection(OnPackageReceived, OnConnectionEstablished, OnConnectionClosed);

            if(_executionThread == null)
            {
                _executionThread = new Thread(ExecuteUserCallbacks)
                                       {
                                           IsBackground = true,
                                           Name = "User callbacks execution thread in sub channel"
                                       };
                _executionThread.Start();
            }
        }

        public void Close()
        {
            if(_connection != null)
                _connection.Close();

            if(_executionThread != null)
                _stopExecutionThread = true;
        }

        public Task Subscribe(string stream, Action<RecordedEvent> eventAppeared, Action subscriptionDropped)
        {
            var id = Guid.NewGuid();
            var source = new TaskCompletionSource<object>();

            if (_subscriptions.TryAdd(id, new Subscription(source, id, stream, eventAppeared, subscriptionDropped)))
            {
                var subscribe = new ClientMessages.SubscribeToStream(stream);
                _connection.EnqueueSend(new TcpPackage(TcpCommand.SubscribeToStream, id, subscribe.Serialize()).AsByteArray());
                return source.Task;
            }

            source.SetException(new Exception("Failed to add subscription. Concurrency failure"));
            return source.Task;
        }

        public void Unsubscribe(string stream)
        {
            var all = _subscriptions.Values;
            var ids = all.Where(s => s.Stream == stream).Select(s => s.Id);

            foreach (var id in ids)
            {
                Subscription removed;
                if(_subscriptions.TryRemove(id, out removed))
                {
                    removed.Source.SetResult(null);
                    ExecuteUserCallbackAsync(removed.SubscriptionDropped);

                    _connection.EnqueueSend(new TcpPackage(TcpCommand.UnsubscribeFromStream,
                                                           id,
                                                           new ClientMessages.UnsubscribeFromStream(stream).Serialize())
                                                .AsByteArray());
                }
            }
        }

        public Task SubscribeToAllStreams(Action<RecordedEvent> eventAppeared, Action subscriptionDropped)
        {
            var id = Guid.NewGuid();
            var source = new TaskCompletionSource<object>();

            if (_subscriptions.TryAdd(id, new Subscription(source, id, eventAppeared, subscriptionDropped)))
            {
                var subscribe = new ClientMessages.SubscribeToAllStreams();
                _connection.EnqueueSend(new TcpPackage(TcpCommand.SubscribeToAllStreams, id, subscribe.Serialize()).AsByteArray());
                return source.Task;
            }

            source.SetException(new Exception("Failed to add subscription to all streams. Concurrency failure"));
            return source.Task;
        }

        public void UnsubscribeFromAllStreams()
        {
            var all = _subscriptions.Values;
            var ids = all.Where(s => s.Stream == null).Select(s => s.Id);

            foreach (var id in ids)
            {
                Subscription removed;
                if (_subscriptions.TryRemove(id, out removed))
                {
                    removed.Source.SetResult(null);
                    ExecuteUserCallbackAsync(removed.SubscriptionDropped);

                    _connection.EnqueueSend(new TcpPackage(TcpCommand.UnsubscribeFromAllStreams,
                                                           id,
                                                           new ClientMessages.UnsubscribeFromAllStreams().Serialize())
                                                .AsByteArray());
                }
            }
        }

        private void ExecuteUserCallbackAsync(Action callback)
        {
            _executionQueue.Enqueue(callback);
        }

        private void ExecuteUserCallbacks()
        {
            while (!_stopExecutionThread)
            {
                Action callback;
                if (_executionQueue.TryDequeue(out callback))
                {
                    try
                    {
                        callback();
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine("User callback thrown : {0}", e);
                    }
                }
                else
                    Thread.Sleep(1);
            }
        }

        private void OnPackageReceived(TcpTypedConnection connection, TcpPackage package)
        {
            Subscription subscription;
            if(!_subscriptions.TryGetValue(package.CorrelationId, out subscription))
            {
                Debug.WriteLine("Unexpected package received : {0} ({1})", package.CorrelationId, package.Command);
                return;
            }

            try
            {
                switch (package.Command)
                {
                    case TcpCommand.StreamEventAppeared:
                        ExecuteUserCallbackAsync(() => subscription.EventAppeared(new RecordedEvent(package.Data.Deserialize<ClientMessages.StreamEventAppeared>())));
                        break;
                    case TcpCommand.SubscriptionDropped:
                    case TcpCommand.SubscriptionToAllDropped:
                        Subscription removed;
                        if(_subscriptions.TryRemove(subscription.Id, out removed))
                        {
                            removed.Source.SetResult(null);
                            ExecuteUserCallbackAsync(removed.SubscriptionDropped);
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(string.Format("Unexpected command : {0}", package.Command));
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("Error on package received : {0}. Stacktrace : {1}", e.Message, e.StackTrace);
            }
        }

        private void OnConnectionEstablished(TcpTypedConnection tcpTypedConnection)
        {
            ConnectedEvent.Set();
        }

        private void OnConnectionClosed(TcpTypedConnection connection, IPEndPoint endPoint, SocketError error)
        {
            ConnectedEvent.Reset();

            var subscriptions = _subscriptions.Values;
            _subscriptions.Clear();

            foreach (var subscription in subscriptions)
            {
                subscription.Source.SetResult(null);
                ExecuteUserCallbackAsync(subscription.SubscriptionDropped);
            }
        }
    }

    internal class ProjectionsManager
    {
        private readonly IPEndPoint _httpEndPoint;
        private readonly HttpAsyncClient _client = new HttpAsyncClient();

        public ProjectionsManager(IPEndPoint httpEndPoint)
        {
            _httpEndPoint = httpEndPoint;
        }

        public Task Enable(string name)
        {
            return SendPost(_httpEndPoint.ToHttpUrl("/projection/{0}/command/enable", name), string.Empty, HttpStatusCode.OK);
        }

        public Task Disable(string name)
        {
            return SendPost(_httpEndPoint.ToHttpUrl("/projection/{0}/command/disable", name), string.Empty, HttpStatusCode.OK);
        }

        public Task CreateOneTime(string query)
        {
            return SendPost(_httpEndPoint.ToHttpUrl("/projections/onetime?type=JS"), query, HttpStatusCode.Created);
        }

        public Task CreateAdHoc(string name, string query)
        {
            return SendPost(_httpEndPoint.ToHttpUrl("/projections/adhoc?name={0}&type=JS", name), query, HttpStatusCode.Created);
        }

        public Task CreateContinious(string name, string query)
        {
            return SendPost(_httpEndPoint.ToHttpUrl("/projections/continuous?name={0}&type=JS", name), query, HttpStatusCode.Created);
        }

        public Task CreatePersistent(string name, string query)
        {
            return SendPost(_httpEndPoint.ToHttpUrl("/projections/persistent?name={0}&type=JS", name), query, HttpStatusCode.Created);
        }

        public Task<string> ListAll()
        {
            return SendGet(_httpEndPoint.ToHttpUrl("/projections/any"), HttpStatusCode.OK);
        }

        public Task<string> ListOneTime()
        {
            return SendGet(_httpEndPoint.ToHttpUrl("/projections/onetime"), HttpStatusCode.OK);
        }

        public Task<string> ListAdHoc()
        {
            return SendGet(_httpEndPoint.ToHttpUrl("/projections/adhoc"), HttpStatusCode.OK);
        }

        public Task<string> ListContinuous()
        {
            return SendGet(_httpEndPoint.ToHttpUrl("/projections/continuous"), HttpStatusCode.OK);
        }

        public Task<string> ListPersistent()
        {
            return SendGet(_httpEndPoint.ToHttpUrl("/projections/persistent"), HttpStatusCode.OK);
        }

        public Task<string> GetStatus(string name)
        {
            return SendGet(_httpEndPoint.ToHttpUrl("/projection/{0}", name), HttpStatusCode.OK);
        }

        public Task<string> GetState(string name)
        {
            return SendGet(_httpEndPoint.ToHttpUrl("/projection/{0}/state", name), HttpStatusCode.OK);
        }

        public Task<string> GetStatistics(string name)
        {
            return SendGet(_httpEndPoint.ToHttpUrl("/projection/{0}/statistics", name), HttpStatusCode.OK);
        }

        public Task<string> GetQuery(string name)
        {
            return SendGet(_httpEndPoint.ToHttpUrl("/projection/{0}/query", name), HttpStatusCode.OK);
        }

        public Task UpdateQuery(string name, string query)
        {
            return SendPut(_httpEndPoint.ToHttpUrl("/projection/{0}/query?type=JS", name), query, HttpStatusCode.OK);
        }

        public Task Delete(string name)
        {
            return SendDelete(_httpEndPoint.ToHttpUrl("/projection/{0}", name), HttpStatusCode.OK);
        }

        private Task<string> SendGet(string url, int expectedCode)
        {
            var source = new TaskCompletionSource<string>();
            _client.Get(url,
                        response =>
                            {
                                if (response.HttpStatusCode == expectedCode)
                                    source.SetResult(response.Body);
                                else
                                    source.SetException(new ProjectionCommandFailedException(
                                                            string.Format("Server returned : {0} ({1})",
                                                                          response.HttpStatusCode,
                                                                          response.StatusDescription)));
                            },
                        source.SetException);

            return source.Task;
        }

        private Task<string> SendDelete(string url, int expectedCode)
        {
            var source = new TaskCompletionSource<string>();
            _client.Delete(url,
                           response =>
                               {
                                   if (response.HttpStatusCode == expectedCode)
                                       source.SetResult(response.Body);
                                   else
                                       source.SetException(new ProjectionCommandFailedException(
                                                               string.Format("Server returned : {0} ({1})",
                                                                             response.HttpStatusCode,
                                                                             response.StatusDescription)));
                               },
                           source.SetException);

            return source.Task;
        }

        private Task SendPut(string url, string content, int expectedCode)
        {
            var source = new TaskCompletionSource<object>();
            _client.Put(url,
                        content,
                        ContentType.Json,
                        response =>
                            {
                                if (response.HttpStatusCode == expectedCode)
                                    source.SetResult(null);
                                else
                                    source.SetException(new ProjectionCommandFailedException(
                                                            string.Format("Server returned : {0} ({1})",
                                                                          response.HttpStatusCode,
                                                                          response.StatusDescription)));
                            },
                        source.SetException);

            return source.Task;
        }

        private Task SendPost(string url, string content, int expectedCode)
        {
            var source = new TaskCompletionSource<object>();
            _client.Post(url,
                         content,
                         ContentType.Json,
                         response =>
                             {
                                 if (response.HttpStatusCode == expectedCode)
                                     source.SetResult(null);
                                 else
                                     source.SetException(new ProjectionCommandFailedException(
                                                             string.Format("Server returned : {0} ({1})",
                                                                           response.HttpStatusCode,
                                                                           response.StatusDescription)));
                             },
                         source.SetException);

            return source.Task;
        }
    }
}