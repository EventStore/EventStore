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
using System.Data.SqlClient;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI.ClientOperations;
using EventStore.ClientAPI.Common;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Core;
using EventStore.ClientAPI.Messages;

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
    internal class EventStoreNodeConnection : IEventStoreConnection, IEventStoreTransactionConnection
    {
        public string ConnectionName { get { return _connectionName; } }

        private readonly string _connectionName;
        private readonly ConnectionSettings _settings;
        private readonly IEndPointDiscoverer _endPointDiscoverer;
        private readonly EventStoreConnectionLogicHandler _handler;

        /// <summary>
        /// Constructs a new instance of a <see cref="EventStoreConnection"/>
        /// </summary>
        /// <param name="settings">The <see cref="ConnectionSettings"/> containing the settings for this connection.</param>
        /// <param name="endPointDiscoverer">Discoverer of destination node end point.</param>
        /// <param name="connectionName">Optional name of connection (will be generated automatically, if not provided)</param>
        internal EventStoreNodeConnection(ConnectionSettings settings, IEndPointDiscoverer endPointDiscoverer, string connectionName)
        {
            Ensure.NotNull(settings, "settings");
            Ensure.NotNull(endPointDiscoverer, "endPointDiscoverer");

            _connectionName = connectionName ?? string.Format("ES-{0}", Guid.NewGuid());
            _settings = settings;
            _endPointDiscoverer = endPointDiscoverer;
            _handler = new EventStoreConnectionLogicHandler(this, settings);
        }

        /// <summary>
        /// Connects the <see cref="EventStoreConnection"/> synchronously to a given <see cref="IPEndPoint"/>
        /// </summary>
        public void Connect()
        {
            ConnectAsync().Wait();
        }

        /// <summary>
        /// Connects the <see cref="EventStoreConnection"/> asynchronously to a given <see cref="IPEndPoint"/>
        /// </summary>
        /// <returns>A <see cref="Task"/> that can be waited upon.</returns>
        public Task ConnectAsync()
        {
            var source = new TaskCompletionSource<object>();
            _handler.EnqueueMessage(new StartConnectionMessage(source, _endPointDiscoverer));
            return source.Task;
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
            _handler.EnqueueMessage(new CloseConnectionMessage("Connection close requested by client.", null));
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

            var source = new TaskCompletionSource<object>();
            EnqueueOperation(new DeleteStreamOperation(_settings.Log, source, _settings.AllowForwarding, stream, expectedVersion));
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

            var source = new TaskCompletionSource<object>();
            EnqueueOperation(new AppendToStreamOperation(_settings.Log, source, _settings.AllowForwarding, stream, expectedVersion, events));
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

            var source = new TaskCompletionSource<EventStoreTransaction>();
            EnqueueOperation(new StartTransactionOperation(_settings.Log, source, _settings.AllowForwarding, stream, expectedVersion, this));
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
        Task IEventStoreTransactionConnection.TransactionalWriteAsync(EventStoreTransaction transaction, IEnumerable<EventData> events)
        {
// ReSharper disable PossibleMultipleEnumeration
            Ensure.NotNull(transaction, "transaction");
            Ensure.NotNull(events, "events");

            var source = new TaskCompletionSource<object>();
            EnqueueOperation(new TransactionalWriteOperation(_settings.Log, source, _settings.AllowForwarding, transaction.TransactionId, events));
            return source.Task;
// ReSharper restore PossibleMultipleEnumeration
        }

        /// <summary>
        /// Commits a multi-write transaction in the Event Store
        /// </summary>
        /// <param name="transaction">The <see cref="EventStoreTransaction"></see> to commit</param>
        /// <returns>A <see cref="Task"/> allowing the caller to control the async operation</returns>
        Task IEventStoreTransactionConnection.CommitTransactionAsync(EventStoreTransaction transaction)
        {
            Ensure.NotNull(transaction, "transaction");

            var source = new TaskCompletionSource<object>();
            EnqueueOperation(new CommitTransactionOperation(_settings.Log, source, _settings.AllowForwarding, transaction.TransactionId));
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

            var source = new TaskCompletionSource<StreamEventsSlice>();
            EnqueueOperation(new ReadStreamEventsForwardOperation(_settings.Log, source, stream, start, count, resolveLinkTos));
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

            var source = new TaskCompletionSource<StreamEventsSlice>();
            EnqueueOperation(new ReadStreamEventsBackwardOperation(_settings.Log, source, stream, start, count, resolveLinkTos));
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

            var source = new TaskCompletionSource<AllEventsSlice>();
            EnqueueOperation(new ReadAllEventsForwardOperation(_settings.Log, source, position, maxCount, resolveLinkTos));
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

            var source = new TaskCompletionSource<AllEventsSlice>();
            EnqueueOperation(new ReadAllEventsBackwardOperation(_settings.Log, source, position, maxCount, resolveLinkTos));
            return source.Task;
        }

        private void EnqueueOperation(IClientOperation operation)
        {
            while (_handler.TotalOperationCount >= _settings.MaxQueueSize)
            {
                Thread.Sleep(1);
            }
            _handler.EnqueueMessage(new StartOperationMessage(operation, _settings.MaxRetries, _settings.OperationTimeout));
        }

        public Task<EventStoreSubscription> SubscribeToStream(string stream,
                                                              bool resolveLinkTos,
                                                              Action<EventStoreSubscription, ResolvedEvent> eventAppeared,
                                                              Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.NotNull(eventAppeared, "eventAppeared");

            var source = new TaskCompletionSource<EventStoreSubscription>();
            _handler.EnqueueMessage(
                                    new StartSubscriptionMessage(source, stream, resolveLinkTos, eventAppeared, subscriptionDropped, 
                                                                 _settings.MaxRetries, _settings.OperationTimeout));
            return source.Task;
        }

        public EventStoreStreamCatchUpSubscription SubscribeToStreamFrom(string stream,
                                                                         int? fromEventNumberExclusive,
                                                                         bool resolveLinkTos,
                                                                         Action<EventStoreCatchUpSubscription, ResolvedEvent> eventAppeared,
                                                                         Action<EventStoreCatchUpSubscription> liveProcessingStarted = null,
                                                                         Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.NotNull(eventAppeared, "eventAppeared");
            var catchUpSubscription = 
                    new EventStoreStreamCatchUpSubscription(this, _settings.Log, stream, fromEventNumberExclusive, 
                                                            resolveLinkTos, eventAppeared, liveProcessingStarted, 
                                                            subscriptionDropped, _settings.VerboseLogging);
            catchUpSubscription.Start();
            return catchUpSubscription;
        }

        public Task<EventStoreSubscription> SubscribeToAll(bool resolveLinkTos, 
                                                           Action<EventStoreSubscription, ResolvedEvent> eventAppeared, 
                                                           Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null)
        {
            Ensure.NotNull(eventAppeared, "eventAppeared");

            var source = new TaskCompletionSource<EventStoreSubscription>();
            _handler.EnqueueMessage(
                                    new StartSubscriptionMessage(source, string.Empty, resolveLinkTos, eventAppeared, subscriptionDropped,
                                                                 _settings.MaxRetries, _settings.OperationTimeout));
            return source.Task;
        }

        public EventStoreAllCatchUpSubscription SubscribeToAllFrom(Position? fromPositionExclusive,
                                                                   bool resolveLinkTos,
                                                                   Action<EventStoreCatchUpSubscription, ResolvedEvent> eventAppeared,
                                                                   Action<EventStoreCatchUpSubscription> liveProcessingStarted = null,
                                                                   Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null)
        {
            Ensure.NotNull(eventAppeared, "eventAppeared");
            var catchUpSubscription = 
                    new EventStoreAllCatchUpSubscription(this, _settings.Log, fromPositionExclusive, resolveLinkTos,
                                                         eventAppeared, liveProcessingStarted, subscriptionDropped, _settings.VerboseLogging);
            catchUpSubscription.Start();
            return catchUpSubscription;
        }


        public void SetStreamMetadata(string stream, int expectedMetastreamVersion, Guid idempotencyId, StreamMetadata metadata)
        {
            SetStreamMetadataAsync(stream, expectedMetastreamVersion, idempotencyId, metadata).Wait();
        }

        public Task SetStreamMetadataAsync(string stream, int expectedMetastreamVersion, Guid idempotencyId, StreamMetadata metadata)
        {
            return SetStreamMetadataAsync(stream, expectedMetastreamVersion, idempotencyId, metadata.AsJsonBytes());
        }

        public void SetStreamMetadata(string stream, int expectedMetastreamVersion, Guid idempotencyId, byte[] metadata)
        {
            SetStreamMetadataAsync(stream, expectedMetastreamVersion, idempotencyId, metadata).Wait();
        }

        public Task SetStreamMetadataAsync(string stream, int expectedMetastreamVersion, Guid idempotencyId, byte[] metadata)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            if (SystemStreams.IsMetastream(stream)) 
                throw new ArgumentException(string.Format("Setting metadata for metastream '{0}' is not supported.", stream), "stream");

            var source = new TaskCompletionSource<object>();

            var metaevent = new EventData(idempotencyId, SystemEventTypes.StreamMetadata, true, metadata ?? Empty.ByteArray, null);
            EnqueueOperation(new AppendToStreamOperation(_settings.Log,
                                                         source,
                                                         _settings.AllowForwarding,
                                                         SystemStreams.MetastreamOf(stream),
                                                         expectedMetastreamVersion,
                                                         new[] { metaevent }));
            return source.Task;
        }

        public StreamMetadataResult GetStreamMetadata(string stream)
        {
            return GetStreamMetadataAsync(stream).Result;
        }

        public Task<StreamMetadataResult> GetStreamMetadataAsync(string stream)
        {
            return GetStreamMetadataAsRawBytesAsync(stream).ContinueWith(t =>
            {
                var res = t.Result;
                if (res.StreamMetadata == null || res.StreamMetadata.Length == 0)
                    return new StreamMetadataResult(res.Stream, res.IsStreamDeleted, res.MetastreamVersion, StreamMetadata.Create());
                var metadata = StreamMetadata.FromJsonBytes(res.StreamMetadata);
                return new StreamMetadataResult(res.Stream, res.IsStreamDeleted, res.MetastreamVersion, metadata);
            });
        }

        public RawStreamMetadataResult GetStreamMetadataAsRawBytes(string stream)
        {
            return GetStreamMetadataAsRawBytesAsync(stream).Result;
        }

        public Task<RawStreamMetadataResult> GetStreamMetadataAsRawBytesAsync(string stream)
        {
            Ensure.NotNullOrEmpty(stream, "stream");

            var source = new TaskCompletionSource<ClientMessage.ReadEventCompleted>();
            EnqueueOperation(new ReadEventOperation(_settings.Log, source, SystemStreams.MetastreamOf(stream), -1, false));
            return source.Task.ContinueWith(t =>
            {
                var res = t.Result;
                switch (res.Result)
                {
                    case ClientMessage.ReadEventCompleted.ReadEventResult.Success:
                        return new RawStreamMetadataResult(stream, false, res.Event.Event.EventNumber, res.Event.Event.Data);
                    case ClientMessage.ReadEventCompleted.ReadEventResult.NotFound:
                    case ClientMessage.ReadEventCompleted.ReadEventResult.NoStream:
                        return new RawStreamMetadataResult(stream, false, -1, Empty.ByteArray);
                    case ClientMessage.ReadEventCompleted.ReadEventResult.StreamDeleted:
                        return new RawStreamMetadataResult(stream, true, int.MaxValue, Empty.ByteArray);
                    default:
                        throw new ArgumentOutOfRangeException(string.Format("Unexpected ReadEventResult: {0}.", res.Result));
                }
            });
        }
    }
}