/*// Copyright (c) 2012, Event Store LLP
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
using System.Data.SqlClient;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI.ClientOperations;
using EventStore.ClientAPI.Common.Log;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Core;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.Transport.Tcp;
using Ensure = EventStore.ClientAPI.Common.Utils.Ensure;
using System.Linq;

namespace EventStore.ClientAPI
{
    /// <summary>
    /// Maintains a full duplex connection to the EventStore
    /// </summary>
    /// <remarks>
    /// An <see cref="EventStoreConnection"/> operates quite differently than say a <see cref="SqlConnection"/>. Normally
    /// when using an <see cref="EventStoreConnection"/> you want to keep the connection open for a much longer of time than 
    /// when you use a SqlConnection. If you prefer the usage pattern of using(new Connection()) .. then you would likely
    /// want to create a FlyWeight on top of the <see cref="EventStoreConnection"/>.
    /// 
    /// Another difference is that with the <see cref="EventStoreConnection"/> all operations are handled in a full async manner
    /// (even if you call the synchronous behaviors). Many threads can use an <see cref="EventStoreConnection"/> at the same
    /// time or a single thread can make many asynchronous requests. To get the most performance out of the connection
    /// it is generally recommended to use it in this way.
    /// </remarks>
    public class EventStoreConnection : IDisposable
    {
        /// <summary>
        /// Raised whenever the internal connection is disconnected from the event store
        /// </summary>
        public event EventHandler Disconnected;
        /// <summary>
        /// Raised whenever the internal connection is reconnecting to the event store
        /// </summary>
        public event EventHandler Reconnecting;
        /// <summary>
        /// Raised whenever the internal connection is connected to the event store
        /// </summary>
        public event EventHandler Connected;

        private readonly ILogger _log;

        private readonly TcpConnector _connector = new TcpConnector();
        private readonly object _connectionLock = new object();
        private IPEndPoint _tcpEndPoint;
        private TcpTypedConnection _connection;
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

        /// <summary>
        /// Constructs a new instance of a <see cref="EventStoreConnection"/>
        /// </summary>
        /// <param name="settings">The <see cref="ConnectionSettings"/> containing the settings for this connection.</param>
        private EventStoreConnection(ConnectionSettings settings)
        {
            _settings = settings;

            LogManager.RegisterLogger(settings.Log);
            _log = LogManager.GetLogger();

            _subscriptionsChannel = new SubscriptionsChannel(_connector);
        }

        /// <summary>
        /// Creates a new <see cref="EventStoreConnection"/> using default <see cref="ConnectionSettings"/>
        /// </summary>
        /// <returns>a new <see cref="EventStoreConnection"/></returns>
        public static EventStoreConnection Create()
        {
            return new EventStoreConnection(ConnectionSettings.Default);
        }

        /// <summary>
        /// Creates a new <see cref="EventStoreConnection"/> using specific <see cref="ConnectionSettings"/>
        /// </summary>
        /// <param name="settings">The <see cref="ConnectionSettings"/> to apply to the new connection</param>
        /// <returns>a new <see cref="EventStoreConnection"/></returns>
        public static EventStoreConnection Create(ConnectionSettings settings)
        {
            Ensure.NotNull(settings, "settings");
            return new EventStoreConnection(settings);
        }

        /// <summary>
        /// Connects the <see cref="EventStoreConnection"/> synchronously to a given <see cref="IPEndPoint"/>
        /// </summary>
        /// <param name="tcpEndPoint">The <see cref="IPEndPoint"/> to connect to</param>
        public void Connect(IPEndPoint tcpEndPoint)
        {
            ConnectAsync(tcpEndPoint).Wait();
        }

        /// <summary>
        /// Connects the <see cref="EventStoreConnection"/> asynchronously to a given <see cref="IPEndPoint"/>
        /// </summary>
        /// <param name="tcpEndPoint">The <see cref="IPEndPoint"/> to connect to.</param>
        /// <returns>A <see cref="Task"/> that can be waited upon.</returns>
        public Task ConnectAsync(IPEndPoint tcpEndPoint)
        {
            Ensure.NotNull(tcpEndPoint, "tcpEndPoint");

            _log.Info("EventStoreConnection: connecting to [{0}]", tcpEndPoint);

            return EstablishConnectionAsync(tcpEndPoint);
        }

        /// <summary>
        /// Connects the <see cref="EventStoreConnection"/> synchronously to a clustered EventStore based upon DNS entry
        /// </summary>
        /// <param name="clusterDns">The cluster's DNS entry.</param>
        /// <param name="maxDiscoverAttempts">The maximum number of attempts to try the DNS.</param>
        /// <param name="port">The port to connect to.</param>
        public void Connect(string clusterDns, int maxDiscoverAttempts = 10, int port = 30777)
        {
            ConnectAsync(clusterDns, maxDiscoverAttempts, port).Wait();
        }

        /// <summary>
        /// Connects the <see cref="EventStoreConnection"/> asynchronously to a clustered EventStore based upon DNS entry
        /// </summary>
        /// <param name="clusterDns">The cluster's DNS entry</param>
        /// <param name="maxDiscoverAttempts">The maximum number of attempts to try the DNS</param>
        /// <param name="port">The port to connect to</param>
        /// <returns>A <see cref="Task"/> that can be awaited upon</returns>
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

                _lastReconnectionTimestamp = GetNow();
                _connection = _connector.CreateTcpConnection(_tcpEndPoint, OnPackageReceived, OnConnectionEstablished, OnConnectionClosed);
                _timeoutCheckStopwatch.Start();

                _worker = new Thread(MainLoop) {IsBackground = true, Name = "EventStoreConnection worker"};
                _worker.Start();

                return Tasks.CreateCompleted();
            }
        }

        private void EnsureActive()
        {
            if (!_active)
                throw new InvalidOperationException(string.Format("EventStoreConnection [{0}] is not active.", _tcpEndPoint));
        }

        /// <summary>
        /// Disposes this <see cref="EventStoreConnection"/>
        /// </summary>
        void IDisposable.Dispose()
        {
            Close();
        }

        /// <summary>
        /// Closes this <see cref="EventStoreConnection"/>
        /// </summary>
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
                workItem.Operation.Fail(new ConnectionClosingException("Work item was still in progress at the moment of manual connection closing."));
            }
            _inProgress.Clear();

            _log.Info("EventStoreConnection closed.");
        }


        /// <summary>
        /// Creates a new Stream in the Event Store synchronously
        /// </summary>
        /// <param name="stream">The name of the stream to create.</param>
        /// <param name="id"></param>
        /// <param name="isJson">A bool whether the metadata is json or not</param>
        /// <param name="metadata">The metadata to associate with the created stream</param>
        public void CreateStream(string stream, Guid id, bool isJson, byte[] metadata)
        {
            CreateStreamAsync(stream, id, isJson, metadata).Wait();
        }

        /// <summary>
        /// Creates a new Stream in the Event Store asynchronously
        /// </summary>
        /// <param name="stream">The name of the stream to create</param>
        /// <param name="id"></param>
        /// <param name="isJson">A bool whether the metadata is json or not.</param>
        /// <param name="metadata">The metadata to associate with the created stream.</param>
        /// <returns>A <see cref="Task"></see> that can be waited upon by the caller.</returns>
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

        /// <summary>
        /// Deletes a stream from the Event Store synchronously
        /// </summary>
        /// <param name="stream">The name of the stream to be deleted</param>
        /// <param name="expectedVersion">The expected version the stream should have when being deleted. <see cref="ExpectedVersion"/></param>
        public void DeleteStream(string stream, int expectedVersion)
        {
            DeleteStreamAsync(stream, expectedVersion).Wait();
        }

        /// <summary>
        /// Deletes a stream from the Event Store asynchronously
        /// </summary>
        /// <param name="stream">The name of the stream to delete.</param>
        /// <param name="expectedVersion">The expected version that the streams should have when being deleted. <see cref="ExpectedVersion"/></param>
        /// <returns>A <see cref="Task"/> that can be awaited upon by the caller.</returns>
        public Task DeleteStreamAsync(string stream, int expectedVersion)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            EnsureActive();

            var source = new TaskCompletionSource<object>();
            var operation = new DeleteStreamOperation(source, Guid.NewGuid(), _settings.AllowForwarding, stream, expectedVersion);

            EnqueueOperation(operation);
            return source.Task;
        }

        /// <summary>
        /// Appends Events synchronously to a stream.
        /// </summary>
        /// <remarks>
        /// When appending events to a stream the <see cref="ExpectedVersion"/> choice can
        /// make a very large difference in the observed behavior. If no stream exists
        /// and ExpectedVersion.Any is used. A new stream will be implicitly created when appending
        /// as an example.
        /// 
        /// There are also differences in idempotency between different types of calls.
        /// If you specify an ExpectedVersion aside from ExpectedVersion.Any the Event Store
        /// will give you an idempotency guarantee. If using ExpectedVersion.Any the Event Store
        /// will do its best to provide idempotency but does not guarantee idempotency.
        /// </remarks>
        /// <param name="stream">The name of the stream to append the events to.</param>
        /// <param name="expectedVersion">The expected version of the stream</param>
        /// <param name="events">The events to write to the stream</param>
        public void AppendToStream(string stream, int expectedVersion, params EventData[] events)
        {
            AppendToStreamAsync(stream, expectedVersion, (IEnumerable<EventData>) events).Wait();
        }

        /// <summary>
        /// Appends Events synchronously to a stream.
        /// </summary>
        /// <remarks>
        /// When appending events to a stream the <see cref="ExpectedVersion"/> choice can
        /// make a very large difference in the observed behavior. If no stream exists
        /// and ExpectedVersion.Any is used. A new stream will be implicitly created when appending
        /// as an example.
        /// 
        /// There are also differences in idempotency between different types of calls.
        /// If you specify an ExpectedVersion aside from ExpectedVersion.Any the Event Store
        /// will give you an idempotency guarantee. If using ExpectedVersion.Any the Event Store
        /// will do its best to provide idempotency but does not guarantee idempotency.
        /// </remarks>
        /// <param name="stream">The name of the stream to append the events to.</param>
        /// <param name="expectedVersion">The expected version of the stream</param>
        /// <param name="events">The events to write to the stream</param>
        public void AppendToStream(string stream, int expectedVersion, IEnumerable<EventData> events)
        {
            AppendToStreamAsync(stream, expectedVersion, events).Wait();
        }

        /// <summary>
        /// Appends Events asynchronously to a stream.
        /// </summary>
        /// <remarks>
        /// When appending events to a stream the <see cref="ExpectedVersion"/> choice can
        /// make a very large difference in the observed behavior. If no stream exists
        /// and ExpectedVersion.Any is used. A new stream will be implicitly created when appending
        /// as an example.
        /// 
        /// There are also differences in idempotency between different types of calls.
        /// If you specify an ExpectedVersion aside from ExpectedVersion.Any the Event Store
        /// will give you an idempotency guarantee. If using ExpectedVersion.Any the Event Store
        /// will do its best to provide idempotency but does not guarantee idempotency
        /// </remarks>
        /// <param name="stream">The name of the stream to append events to</param>
        /// <param name="expectedVersion">The <see cref="ExpectedVersion"/> of the stream to append to</param>
        /// <param name="events">The events to append to the stream</param>
        /// <returns>a <see cref="Task"/> that the caller can await on.</returns>
        public Task AppendToStreamAsync(string stream, int expectedVersion, params EventData[] events)
        {
            return AppendToStreamAsync(stream, expectedVersion, (IEnumerable<EventData>) events);
        }

        /// <summary>
        /// Appends Events asynchronously to a stream.
        /// </summary>
        /// <remarks>
        /// When appending events to a stream the <see cref="ExpectedVersion"/> choice can
        /// make a very large difference in the observed behavior. If no stream exists
        /// and ExpectedVersion.Any is used. A new stream will be implicitly created when appending
        /// as an example.
        /// 
        /// There are also differences in idempotency between different types of calls.
        /// If you specify an ExpectedVersion aside from ExpectedVersion.Any the Event Store
        /// will give you an idempotency guarantee. If using ExpectedVersion.Any the Event Store
        /// will do its best to provide idempotency but does not guarantee idempotency
        /// </remarks>
        /// <param name="stream">The name of the stream to append events to</param>
        /// <param name="expectedVersion">The <see cref="ExpectedVersion"/> of the stream to append to</param>
        /// <param name="events">The events to append to the stream</param>
        /// <returns>a <see cref="Task"/> that the caller can await on.</returns>
        public Task AppendToStreamAsync(string stream, int expectedVersion, IEnumerable<EventData> events)
        {
// ReSharper disable PossibleMultipleEnumeration
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.NotNull(events, "events");
            EnsureActive();

            var source = new TaskCompletionSource<object>();
            var operation = new AppendToStreamOperation(source, Guid.NewGuid(), _settings.AllowForwarding, stream, expectedVersion, events);

            EnqueueOperation(operation);
            return source.Task;
// ReSharper restore PossibleMultipleEnumeration
        }


        /// <summary>
        /// Starts a transaction in the event store on a given stream
        /// </summary>
        /// <remarks>
        /// A <see cref="EventStoreTransaction"/> allows the calling of multiple writes with multiple
        /// round trips over long periods of time between the caller and the event store. This method
        /// is only available through the TCP interface and no equivalent exists for the RESTful interface.
        /// </remarks>
        /// <param name="stream">The stream to start a transaction on</param>
        /// <param name="expectedVersion">The expected version when starting a transaction</param>
        /// <returns>An <see cref="EventStoreTransaction"/> that can be used to control a series of operations.</returns>
        public EventStoreTransaction StartTransaction(string stream, int expectedVersion)
        {
            return StartTransactionAsync(stream, expectedVersion).Result;
        }

        /// <summary>
        /// Starts a transaction in the event store on a given stream asynchronously
        /// </summary>
        /// <remarks>
        /// A <see cref="EventStoreTransaction"/> allows the calling of multiple writes with multiple
        /// round trips over long periods of time between the caller and the event store. This method
        /// is only available through the TCP interface and no equivalent exists for the RESTful interface.
        /// </remarks>
        /// <param name="stream">The stream to start a transaction on</param>
        /// <param name="expectedVersion">The expected version of the stream at the time of starting the transaction</param>
        /// <returns>A task the caller can use to control the operation.</returns>
        public Task<EventStoreTransaction> StartTransactionAsync(string stream, int expectedVersion)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            EnsureActive();

            var source = new TaskCompletionSource<EventStoreTransaction>();
            var operation = new StartTransactionOperation(source, Guid.NewGuid(), _settings.AllowForwarding, stream, expectedVersion, this);

            EnqueueOperation(operation);
            return source.Task;
        }

        /// <summary>
        /// Continues transaction by provided transaction ID.
        /// </summary>
        /// <remarks>
        /// A <see cref="EventStoreTransaction"/> allows the calling of multiple writes with multiple
        /// round trips over long periods of time between the caller and the event store. This method
        /// is only available through the TCP interface and no equivalent exists for the RESTful interface.
        /// </remarks>
        /// <param name="transactionId">The transaction ID that needs to be continued.</param>
        /// <returns><see cref="EventStoreTransaction"/> object.</returns>
        public EventStoreTransaction ContinueTransaction(long transactionId)
        {
            Ensure.Nonnegative(transactionId, "transactionId");
            return new EventStoreTransaction(transactionId, this);
        }

        /// <summary>
        /// Writes to a transaction in the event store asynchronously
        /// </summary>
        /// <remarks>
        /// A <see cref="EventStoreTransaction"/> allows the calling of multiple writes with multiple
        /// round trips over long periods of time between the caller and the event store. This method
        /// is only available through the TCP interface and no equivalent exists for the RESTful interface.
        /// </remarks>
        /// <param name="transaction">The <see cref="EventStoreTransaction"/> to write to.</param>
        /// <param name="events">The events to write</param>
        /// <returns>A <see cref="Task"/> allowing the caller to control the async operation</returns>
        internal Task TransactionalWriteAsync(EventStoreTransaction transaction, IEnumerable<EventData> events)
        {
// ReSharper disable PossibleMultipleEnumeration
            Ensure.NotNull(transaction, "transaction");
            Ensure.NotNull(events, "events");
            EnsureActive();

            var source = new TaskCompletionSource<object>();
            var operation = new TransactionalWriteOperation(source, Guid.NewGuid(), _settings.AllowForwarding, transaction.TransactionId, events);

            EnqueueOperation(operation);
            return source.Task;
// ReSharper restore PossibleMultipleEnumeration
        }

        /// <summary>
        /// Commits a multi-write transaction in the Event Store
        /// </summary>
        /// <param name="transaction">The <see cref="EventStoreTransaction"></see> to commit</param>
        /// <returns>A <see cref="Task"/> allowing the caller to control the async operation</returns>
        internal Task CommitTransactionAsync(EventStoreTransaction transaction)
        {
            Ensure.NotNull(transaction, "transaction");
            EnsureActive();

            var source = new TaskCompletionSource<object>();
            var operation = new CommitTransactionOperation(source, Guid.NewGuid(), _settings.AllowForwarding, transaction.TransactionId);

            EnqueueOperation(operation);
            return source.Task;
        }

        /// <summary>
        /// Reads count Events from an Event Stream forwards (e.g. oldest to newest) starting from position start
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <param name="start">The starting point to read from</param>
        /// <param name="count">The count of items to read</param>
        /// <param name="resolveLinkTos">Whether to resolve LinkTo events automatically</param>
        /// <returns>A <see cref="StreamEventsSlice"/> containing the results of the read operation</returns>
        public StreamEventsSlice ReadStreamEventsForward(string stream, int start, int count, bool resolveLinkTos)
        {
            return ReadStreamEventsForwardAsync(stream, start, count, resolveLinkTos).Result;
        }

        /// <summary>
        /// Reads count Events from an Event Stream forwards (e.g. oldest to newest) starting from position start 
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <param name="start">The starting point to read from</param>
        /// <param name="count">The count of items to read</param>
        /// <param name="resolveLinkTos">Whether to resolve LinkTo events automatically</param>
        /// <returns>A <see cref="Task&lt;StreamEventsSlice&gt;"/> containing the results of the read operation</returns>
        public Task<StreamEventsSlice> ReadStreamEventsForwardAsync(string stream, int start, int count, bool resolveLinkTos)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.Nonnegative(start, "start");
            Ensure.Positive(count, "count");
            EnsureActive();

            var source = new TaskCompletionSource<StreamEventsSlice>();
            var operation = new ReadStreamEventsForwardOperation(source, Guid.NewGuid(), stream, start, count, resolveLinkTos);

            EnqueueOperation(operation);
            return source.Task;
        }

        /// <summary>
        /// Reads count events from an Event Stream backwards (e.g. newest to oldest) from position
        /// </summary>
        /// <param name="stream">The Event Stream to read from</param>
        /// <param name="start">The position to start reading from</param>
        /// <param name="count">The count to read from the position</param>
        /// <param name="resolveLinkTos">Whether to resolve LinkTo events automatically</param>
        /// <returns>An <see cref="StreamEventsSlice"/> containing the results of the read operation</returns>
        public StreamEventsSlice ReadStreamEventsBackward(string stream, int start, int count, bool resolveLinkTos)
        {
            return ReadStreamEventsBackwardAsync(stream, start, count, resolveLinkTos).Result;
        }

        /// <summary>
        /// Reads count events from an Event Stream backwards (e.g. newest to oldest) from position asynchronously
        /// </summary>
        /// <param name="stream">The Event Stream to read from</param>
        /// <param name="start">The position to start reading from</param>
        /// <param name="count">The count to read from the position</param>
        /// <param name="resolveLinkTos">Whether to resolve LinkTo events automatically</param>
        /// <returns>An <see cref="Task&lt;StreamEventsSlice&gt;"/> containing the results of the read operation</returns>
        public Task<StreamEventsSlice> ReadStreamEventsBackwardAsync(string stream, int start, int count, bool resolveLinkTos)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.Positive(count, "count");
            EnsureActive();

            var source = new TaskCompletionSource<StreamEventsSlice>();
            var operation = new ReadStreamEventsBackwardOperation(source, Guid.NewGuid(), stream, start, count, resolveLinkTos);

            EnqueueOperation(operation);
            return source.Task;
        }


        /// <summary>
        /// Reads All Events in the node forward. (e.g. beginning to end)
        /// </summary>
        /// <param name="position">The position to start reading from</param>
        /// <param name="maxCount">The maximum count to read</param>
        /// <param name="resolveLinkTos">Whether to resolve LinkTo events automatically</param>
        /// <returns>A <see cref="AllEventsSlice"/> containing the records read</returns>
        public AllEventsSlice ReadAllEventsForward(Position position, int maxCount, bool resolveLinkTos)
        {
            return ReadAllEventsForwardAsync(position, maxCount, resolveLinkTos).Result;
        }

        /// <summary>
        /// Reads All Events in the node forward asynchronously (e.g. beginning to end)
        /// </summary>
        /// <param name="position">The position to start reading from</param>
        /// <param name="maxCount">The maximum count to read</param>
        /// <param name="resolveLinkTos">Whether to resolve LinkTo events automatically</param>
        /// <returns>A <see cref="AllEventsSlice"/> containing the records read</returns>
        public Task<AllEventsSlice> ReadAllEventsForwardAsync(Position position, int maxCount, bool resolveLinkTos)
        {
            Ensure.Positive(maxCount, "maxCount");
            EnsureActive();

            var source = new TaskCompletionSource<AllEventsSlice>();
            var operation = new ReadAllEventsForwardOperation(source, Guid.NewGuid(), position, maxCount, resolveLinkTos);

            EnqueueOperation(operation);
            return source.Task;
        }

        /// <summary>
        /// Reads All Events in the node backwards (e.g. end to beginning)
        /// </summary>
        /// <param name="position">The position to start reading from</param>
        /// <param name="maxCount">The maximum count to read</param>
        /// <param name="resolveLinkTos">Whether to resolve LinkTo events automatically</param>
        /// <returns>A <see cref="AllEventsSlice"/> containing the records read</returns>
        public AllEventsSlice ReadAllEventsBackward(Position position, int maxCount, bool resolveLinkTos)
        {
            return ReadAllEventsBackwardAsync(position, maxCount, resolveLinkTos).Result;
        }

        /// <summary>
        /// Reads All Events in the node backwards (e.g. end to beginning)
        /// </summary>
        /// <param name="position">The position to start reading from</param>
        /// <param name="maxCount">The maximum count to read</param>
        /// <param name="resolveLinkTos">Whether to resolve LinkTo events automatically</param>
        /// <returns>A <see cref="AllEventsSlice"/> containing the records read</returns>
        public Task<AllEventsSlice> ReadAllEventsBackwardAsync(Position position, int maxCount, bool resolveLinkTos)
        {
            Ensure.Positive(maxCount, "maxCount");
            EnsureActive();

            var source = new TaskCompletionSource<AllEventsSlice>();
            var operation = new ReadAllEventsBackwardOperation(source, Guid.NewGuid(), position, maxCount, resolveLinkTos);

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

        public Task<EventStoreSubscription> SubscribeToStream(string stream, 
                                                              bool resolveLinkTos, 
                                                              Action<ResolvedEvent> eventAppeared, 
                                                              Action subscriptionDropped = null)
        {
            Ensure.NotNull(eventAppeared, "eventAppeared");
            return SubscribeToStream(stream,
                                     resolveLinkTos,
                                     (subscr, e) => eventAppeared(e),
                                     subscriptionDropped == null ? (Action<EventStoreSubscription>)null : subscr => subscriptionDropped());
        }

        public Task<EventStoreSubscription> SubscribeToStream(string stream,
                                                              bool resolveLinkTos,
                                                              Action<EventStoreSubscription, ResolvedEvent> eventAppeared,
                                                              Action<EventStoreSubscription> subscriptionDropped = null)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.NotNull(eventAppeared, "eventAppeared");
            EnsureActive();
            _subscriptionsChannel.EnsureConnected(_tcpEndPoint);
            return _subscriptionsChannel.Subscribe(stream, resolveLinkTos, eventAppeared, subscriptionDropped);
        }

        public EventStoreStreamCatchUpSubscription SubscribeToStreamFrom(string stream,
                                                                         int? fromEventNumberExclusive,
                                                                         bool resolveLinkTos,
                                                                         Action<EventStoreCatchUpSubscription, ResolvedEvent> eventAppeared,
                                                                         Action<EventStoreCatchUpSubscription, string, Exception> subscriptionDropped = null)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.NotNull(eventAppeared, "eventAppeared");
            var catchUpSubscription = new EventStoreStreamCatchUpSubscription(this,
                                                                              stream,
                                                                              fromEventNumberExclusive,
                                                                              resolveLinkTos,
                                                                              eventAppeared,
                                                                              subscriptionDropped);
            catchUpSubscription.Start();
            return catchUpSubscription;
        }

        public Task<EventStoreSubscription> SubscribeToAll(bool resolveLinkTos,
                                                           Action<ResolvedEvent> eventAppeared,
                                                           Action subscriptionDropped = null)
        {
            Ensure.NotNull(eventAppeared, "eventAppeared");
            return SubscribeToAll(resolveLinkTos,
                                  (subscr, e) => eventAppeared(e),
                                  subscriptionDropped == null ? (Action<EventStoreSubscription>)null : subscr => subscriptionDropped());
        }

        public Task<EventStoreSubscription> SubscribeToAll(bool resolveLinkTos, 
                                                           Action<EventStoreSubscription, ResolvedEvent> eventAppeared, 
                                                           Action<EventStoreSubscription> subscriptionDropped = null)
        {
            Ensure.NotNull(eventAppeared, "eventAppeared");
            EnsureActive();
            _subscriptionsChannel.EnsureConnected(_tcpEndPoint);
            return _subscriptionsChannel.Subscribe(string.Empty, resolveLinkTos, eventAppeared, subscriptionDropped);
        }

        public EventStoreAllCatchUpSubscription SubscribeToAllFrom(Position? fromPositionExclusive,
                                                                   bool resolveLinkTos,
                                                                   Action<EventStoreCatchUpSubscription, ResolvedEvent> eventAppeared,
                                                                   Action<EventStoreCatchUpSubscription, string, Exception> subscriptionDropped = null)
        {
            Ensure.NotNull(eventAppeared, "eventAppeared");
            var catchUpSubscription = new EventStoreAllCatchUpSubscription(this,
                                                                             fromPositionExclusive,
                                                                             resolveLinkTos,
                                                                             eventAppeared,
                                                                             subscriptionDropped);
            catchUpSubscription.Start();
            return catchUpSubscription;
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

                        OnReconnecting();

                        _lastReconnectionTimestamp = GetNow();
                        _connection = _connector.CreateTcpConnection(_tcpEndPoint, OnPackageReceived, OnConnectionEstablished, OnConnectionClosed);
                        _reconnectionStopwatch.Stop();
                    }
                }

                if (_timeoutCheckStopwatch.Elapsed > _settings.OperationTimeoutCheckPeriod)
                {
                    var now = GetNow();
                    var retriable = new List<WorkItem>();
                    foreach (var workerItem in _inProgress.Values)
                    {
                        var lastUpdated = new DateTime(Interlocked.Read(ref workerItem.LastUpdatedTicks));
                        if (now - lastUpdated > _settings.OperationTimeout)
                        {
                            if (lastUpdated >= _lastReconnectionTimestamp)
                            {
                                var err = string.Format("{0} never got response from server" +
                                                        "Last state update: {1:HH:mm:ss.fff}, last reconnect: {2:HH:mm:ss.fff}, UTC now: {3:HH:mm:ss.fff}.",
                                                        workerItem, lastUpdated, _lastReconnectionTimestamp, now);
                                _log.Error(err);
                                if (TryRemoveWorkItem(workerItem))
                                    workerItem.Operation.Fail(new OperationTimedOutException(err));
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
                        Interlocked.Exchange(ref inProgressItem.LastUpdatedTicks, GetNow().Ticks);
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
                    _subscriptionsChannel.Close();
                }
                Retry(workItem);
            }
        }

        private void OnPackageReceived(TcpTypedConnection connection, TcpPackage package)
        {
            if (package.Command == TcpCommand.BadRequest && package.CorrelationId == Guid.Empty)
            {
                string message = Helper.EatException(() => Encoding.UTF8.GetString(package.Data.Array, package.Data.Offset, package.Data.Count));
                try
                {
                    throw new EventStoreConnectionException(
                        string.Format("BadRequest received from server. Error: {0}", string.IsNullOrEmpty(message) ? "<no message>" : message));
                }
                catch (EventStoreConnectionException exc)
                {
                    OnError(exc);
                }
                return;
            }

            WorkItem workItem;
            if (!_inProgress.TryGetValue(package.CorrelationId, out workItem))
            {
                _log.Error("Unexpected CorrelationId received {{{0}}}", package.CorrelationId);
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
                    throw new ArgumentOutOfRangeException(string.Format("Unknown InspectionDecision: {0}.", result.Decision));
            }
        }

        private void OnConnectionEstablished(TcpTypedConnection tcpTypedConnection)
        {
            lock (_connectionLock)
            {
                _reconnectionsCount = 0;
            }
            OnConnected();
        }

        private void OnConnectionClosed(TcpTypedConnection connection, IPEndPoint endPoint, SocketError error)
        {
            OnDisconnected();
            lock (_connectionLock)
            {
                _reconnectionStopwatch.Restart();
            }
        }

        private void OnError(Exception exc)
        {
            if (_settings.ErrorOccurred != null)
                _settings.ErrorOccurred(this, exc);
        }

        private void OnConnected()
        {
            if (_settings.Connected != null)
                _settings.Connected(this);

            var handler = Connected;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        private void OnDisconnected()
        {
            if (_settings.Disconnected != null)
                _settings.Disconnected(this);

            var handler = Disconnected;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        private void OnReconnecting()
        {
            if (_settings.Reconnecting != null)
                _settings.Reconnecting(this);

            var handler = Reconnecting;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        private readonly object _timeLock = new object();
        private readonly DateTime _startTime = DateTime.UtcNow;
        private readonly Stopwatch _watch = Stopwatch.StartNew();
             
        private DateTime GetNow()
        {
            lock (_timeLock)
            {
                return _startTime + _watch.Elapsed;
            }
        }
    }
}*/