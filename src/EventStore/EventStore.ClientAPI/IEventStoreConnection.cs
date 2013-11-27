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
using System.Threading.Tasks;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI
{
    /// <summary>
    /// Maintains a full duplex connection to the EventStore
    /// </summary>
    /// <remarks>
    /// An <see cref="IEventStoreConnection"/> operates quite differently than say a <see cref="SqlConnection"/>. Normally
    /// when using an <see cref="IEventStoreConnection"/> you want to keep the connection open for a much longer of time than 
    /// when you use a SqlConnection. If you prefer the usage pattern of using(new Connection()) .. then you would likely
    /// want to create a FlyWeight on top of the <see cref="EventStoreConnection"/>.
    /// 
    /// Another difference is that with the <see cref="IEventStoreConnection"/> all operations are handled in a full async manner
    /// (even if you call the synchronous behaviors). Many threads can use an <see cref="IEventStoreConnection"/> at the same
    /// time or a single thread can make many asynchronous requests. To get the most performance out of the connection
    /// it is generally recommended to use it in this way.
    /// </remarks>
    public interface IEventStoreConnection : IDisposable
    {
        /// <summary>
        /// Gets the name of this connection. A connection name can be used for disambiguation
        /// in log files.
        /// </summary>
        string ConnectionName { get; }

        /// <summary>
        /// Connects the <see cref="IEventStoreConnection"/> synchronously to a destination
        /// </summary>
        void Connect();

        /// <summary>
        /// Connects the <see cref="IEventStoreConnection"/> asynchronously to a destination
        /// </summary>
        /// <returns>A <see cref="Task"/> that can be waited upon.</returns>
        Task ConnectAsync();

        /// <summary>
        /// Closes this <see cref="IEventStoreConnection"/>
        /// </summary>
        void Close();

        /// <summary>
        /// Deletes a stream from the Event Store synchronously
        /// </summary>
        /// <param name="stream">The name of the stream to be deleted</param>
        /// <param name="expectedVersion">The expected version the stream should have when being deleted. <see cref="ExpectedVersion"/></param>
        /// <param name="userCredentials">The optional user credentials to perform operation with.</param>
        void DeleteStream(string stream, int expectedVersion, UserCredentials userCredentials = null);

        /// <summary>
        /// Deletes a stream from the Event Store synchronously
        /// </summary>
        /// <param name="stream">The name of the stream to be deleted</param>
        /// <param name="expectedVersion">The expected version the stream should have when being deleted. <see cref="ExpectedVersion"/></param>
        /// <param name="hardDelete">Indicator for tombstoning vs soft-deleting the stream. Tombstoned streams can never be recreated. Soft-deleted streams
        /// can be written to again, but the EventNumber sequence will not start from 0.</param>
        /// <param name="userCredentials">The optional user credentials to perform operation with.</param>
        void DeleteStream(string stream, int expectedVersion, bool hardDelete, UserCredentials userCredentials = null);

        /// <summary>
        /// Deletes a stream from the Event Store asynchronously
        /// </summary>
        /// <param name="stream">The name of the stream to delete.</param>
        /// <param name="expectedVersion">The expected version that the streams should have when being deleted. <see cref="ExpectedVersion"/></param>
        /// <param name="userCredentials">The optional user credentials to perform operation with.</param>
        /// <returns>A <see cref="Task"/> that can be awaited upon by the caller.</returns>
        Task DeleteStreamAsync(string stream, int expectedVersion, UserCredentials userCredentials = null);

        /// <summary>
        /// Deletes a stream from the Event Store asynchronously
        /// </summary>
        /// <param name="stream">The name of the stream to delete.</param>
        /// <param name="expectedVersion">The expected version that the streams should have when being deleted. <see cref="ExpectedVersion"/></param>
        /// <param name="hardDelete">Indicator for tombstoning vs soft-deleting the stream. Tombstoned streams can never be recreated. Soft-deleted streams
        /// can be written to again, but the EventNumber sequence will not start from 0.</param>
        /// <param name="userCredentials">The optional user credentials to perform operation with.</param>
        /// <returns>A <see cref="Task"/> that can be awaited upon by the caller.</returns>
        Task DeleteStreamAsync(string stream, int expectedVersion, bool hardDelete, UserCredentials userCredentials = null);

        /// <summary>
        /// Appends Events synchronously to a stream.
        /// </summary>
        /// <remarks>
        /// When appending events to a stream the <see cref="ExpectedVersion"/> choice can
        /// make a very large difference in the observed behavior. For example, if no stream exists
        /// and ExpectedVersion.Any is used, a new stream will be implicitly created when appending.
        /// 
        /// There are also differences in idempotency between different types of calls.
        /// If you specify an ExpectedVersion aside from ExpectedVersion.Any the Event Store
        /// will give you an idempotency guarantee. If using ExpectedVersion.Any the Event Store
        /// will do its best to provide idempotency but does not guarantee idempotency.
        /// </remarks>
        /// <param name="stream">The name of the stream to append the events to.</param>
        /// <param name="expectedVersion">The expected version of the stream</param>
        /// <param name="events">The events to write to the stream</param>
        WriteResult AppendToStream(string stream, int expectedVersion, params EventData[] events);

        /// <summary>
        /// Appends Events synchronously to a stream.
        /// </summary>
        /// <remarks>
        /// When appending events to a stream the <see cref="ExpectedVersion"/> choice can
        /// make a very large difference in the observed behavior. For example, if no stream exists
        /// and ExpectedVersion.Any is used, a new stream will be implicitly created when appending.
        /// 
        /// There are also differences in idempotency between different types of calls.
        /// If you specify an ExpectedVersion aside from ExpectedVersion.Any the Event Store
        /// will give you an idempotency guarantee. If using ExpectedVersion.Any the Event Store
        /// will do its best to provide idempotency but does not guarantee idempotency.
        /// </remarks>
        /// <param name="stream">The name of the stream to append the events to.</param>
        /// <param name="expectedVersion">The expected version of the stream</param>
        /// <param name="userCredentials">The optional user credentials to perform operation with.</param>
        /// <param name="events">The events to write to the stream</param>
        WriteResult AppendToStream(string stream, int expectedVersion, UserCredentials userCredentials, params EventData[] events);

        /// <summary>
        /// Appends Events synchronously to a stream.
        /// </summary>
        /// <remarks>
        /// When appending events to a stream the <see cref="ExpectedVersion"/> choice can
        /// make a very large difference in the observed behavior. For example, if no stream exists
        /// and ExpectedVersion.Any is used, a new stream will be implicitly created when appending.
        /// 
        /// There are also differences in idempotency between different types of calls.
        /// If you specify an ExpectedVersion aside from ExpectedVersion.Any the Event Store
        /// will give you an idempotency guarantee. If using ExpectedVersion.Any the Event Store
        /// will do its best to provide idempotency but does not guarantee idempotency.
        /// </remarks>
        /// <param name="stream">The name of the stream to append the events to.</param>
        /// <param name="expectedVersion">The expected version of the stream</param>
        /// <param name="events">The events to write to the stream</param>
        /// <param name="userCredentials">The optional user credentials to perform operation with.</param>
        WriteResult AppendToStream(string stream, int expectedVersion, IEnumerable<EventData> events, UserCredentials userCredentials = null);

        /// <summary>
        /// Appends Events asynchronously to a stream.
        /// </summary>
        /// <remarks>
        /// When appending events to a stream the <see cref="ExpectedVersion"/> choice can
        /// make a very large difference in the observed behavior. For example, if no stream exists
        /// and ExpectedVersion.Any is used, a new stream will be implicitly created when appending.
        /// 
        /// There are also differences in idempotency between different types of calls.
        /// If you specify an ExpectedVersion aside from ExpectedVersion.Any the Event Store
        /// will give you an idempotency guarantee. If using ExpectedVersion.Any the Event Store
        /// will do its best to provide idempotency but does not guarantee idempotency
        /// </remarks>
        /// <param name="stream">The name of the stream to append events to</param>
        /// <param name="expectedVersion">The <see cref="ExpectedVersion"/> of the stream to append to</param>
        /// <param name="events">The events to append to the stream</param>
        Task<WriteResult> AppendToStreamAsync(string stream, int expectedVersion, params EventData[] events);

        /// <summary>
        /// Appends Events asynchronously to a stream.
        /// </summary>
        /// <remarks>
        /// When appending events to a stream the <see cref="ExpectedVersion"/> choice can
        /// make a very large difference in the observed behavior. For example, if no stream exists
        /// and ExpectedVersion.Any is used, a new stream will be implicitly created when appending.
        /// 
        /// There are also differences in idempotency between different types of calls.
        /// If you specify an ExpectedVersion aside from ExpectedVersion.Any the Event Store
        /// will give you an idempotency guarantee. If using ExpectedVersion.Any the Event Store
        /// will do its best to provide idempotency but does not guarantee idempotency
        /// </remarks>
        /// <param name="stream">The name of the stream to append events to</param>
        /// <param name="expectedVersion">The <see cref="ExpectedVersion"/> of the stream to append to</param>
        /// <param name="userCredentials">The optional user credentials to perform operation with.</param>
        /// <param name="events">The events to append to the stream</param>
        Task<WriteResult> AppendToStreamAsync(string stream, int expectedVersion, UserCredentials userCredentials, params EventData[] events);

        /// <summary>
        /// Appends Events asynchronously to a stream.
        /// </summary>
        /// <remarks>
        /// When appending events to a stream the <see cref="ExpectedVersion"/> choice can
        /// make a very large difference in the observed behavior. For example, if no stream exists
        /// and ExpectedVersion.Any is used, a new stream will be implicitly created when appending.
        /// 
        /// There are also differences in idempotency between different types of calls.
        /// If you specify an ExpectedVersion aside from ExpectedVersion.Any the Event Store
        /// will give you an idempotency guarantee. If using ExpectedVersion.Any the Event Store
        /// will do its best to provide idempotency but does not guarantee idempotency
        /// </remarks>
        /// <param name="stream">The name of the stream to append events to</param>
        /// <param name="expectedVersion">The <see cref="ExpectedVersion"/> of the stream to append to</param>
        /// <param name="events">The events to append to the stream</param>
        /// <param name="userCredentials">The optional user credentials to perform operation with.</param>
        Task<WriteResult> AppendToStreamAsync(string stream, int expectedVersion, IEnumerable<EventData> events, UserCredentials userCredentials = null);

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
        /// <param name="userCredentials">The optional user credentials to perform operation with.</param>
        /// <returns>An <see cref="EventStoreTransaction"/> that can be used to control a series of operations.</returns>
        EventStoreTransaction StartTransaction(string stream, int expectedVersion, UserCredentials userCredentials = null);

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
        /// <param name="userCredentials">The optional user credentials to perform operation with.</param>
        /// <returns>A task the caller can use to control the operation.</returns>
        Task<EventStoreTransaction> StartTransactionAsync(string stream, int expectedVersion, UserCredentials userCredentials = null);

        /// <summary>
        /// Continues transaction by provided transaction ID.
        /// </summary>
        /// <remarks>
        /// A <see cref="EventStoreTransaction"/> allows the calling of multiple writes with multiple
        /// round trips over long periods of time between the caller and the event store. This method
        /// is only available through the TCP interface and no equivalent exists for the RESTful interface.
        /// </remarks>
        /// <param name="transactionId">The transaction ID that needs to be continued.</param>
        /// <param name="userCredentials">The optional user credentials to perform operation with.</param>
        /// <returns><see cref="EventStoreTransaction"/> object.</returns>
        EventStoreTransaction ContinueTransaction(long transactionId, UserCredentials userCredentials = null);

        /// <summary>
        /// Reads count Events from an Event Stream forwards (e.g. oldest to newest) starting from position start
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <param name="eventNumber">The event number to read, -1 (<see cref="StreamPosition">StreamPosition.End</see>) for reading latest event in the stream</param>
        /// <param name="resolveLinkTos">Whether to resolve LinkTo events automatically</param>
        /// <param name="userCredentials">The optional user credentials to perform operation with.</param>
        /// <returns>A <see cref="EventReadResult"/> containing the results of the read operation</returns>
        EventReadResult ReadEvent(string stream, int eventNumber, bool resolveLinkTos, UserCredentials userCredentials = null);

        /// <summary>
        /// Reads count Events from an Event Stream forwards (e.g. oldest to newest) starting from position start
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <param name="eventNumber">The event number to read, -1 (<see cref="StreamPosition">StreamPosition.End</see>) for reading latest event in the stream</param>
        /// <param name="resolveLinkTos">Whether to resolve LinkTo events automatically</param>
        /// <param name="userCredentials">The optional user credentials to perform operation with.</param>
        /// <returns>A <see cref="Task&lt;EventReadResult&gt;"/> containing the results of the read operation</returns>
        Task<EventReadResult> ReadEventAsync(string stream, int eventNumber, bool resolveLinkTos, UserCredentials userCredentials = null);

        /// <summary>
        /// Reads count Events from an Event Stream forwards (e.g. oldest to newest) starting from position start
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <param name="start">The starting point to read from</param>
        /// <param name="count">The count of items to read</param>
        /// <param name="resolveLinkTos">Whether to resolve LinkTo events automatically</param>
        /// <param name="userCredentials">The optional user credentials to perform operation with.</param>
        /// <returns>A <see cref="StreamEventsSlice"/> containing the results of the read operation</returns>
        StreamEventsSlice ReadStreamEventsForward(string stream, int start, int count, bool resolveLinkTos, UserCredentials userCredentials = null);

        /// <summary>
        /// Reads count Events from an Event Stream forwards (e.g. oldest to newest) starting from position start 
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <param name="start">The starting point to read from</param>
        /// <param name="count">The count of items to read</param>
        /// <param name="resolveLinkTos">Whether to resolve LinkTo events automatically</param>
        /// <param name="userCredentials">The optional user credentials to perform operation with.</param>
        /// <returns>A <see cref="Task&lt;StreamEventsSlice&gt;"/> containing the results of the read operation</returns>
        Task<StreamEventsSlice> ReadStreamEventsForwardAsync(string stream, int start, int count, bool resolveLinkTos, UserCredentials userCredentials = null);

        /// <summary>
        /// Reads count events from an Event Stream backwards (e.g. newest to oldest) from position
        /// </summary>
        /// <param name="stream">The Event Stream to read from</param>
        /// <param name="start">The position to start reading from</param>
        /// <param name="count">The count to read from the position</param>
        /// <param name="resolveLinkTos">Whether to resolve LinkTo events automatically</param>
        /// <param name="userCredentials">The optional user credentials to perform operation with.</param>
        /// <returns>An <see cref="StreamEventsSlice"/> containing the results of the read operation</returns>
        StreamEventsSlice ReadStreamEventsBackward(string stream, int start, int count, bool resolveLinkTos, UserCredentials userCredentials = null);

        /// <summary>
        /// Reads count events from an Event Stream backwards (e.g. newest to oldest) from position asynchronously
        /// </summary>
        /// <param name="stream">The Event Stream to read from</param>
        /// <param name="start">The position to start reading from</param>
        /// <param name="count">The count to read from the position</param>
        /// <param name="resolveLinkTos">Whether to resolve LinkTo events automatically</param>
        /// <param name="userCredentials">The optional user credentials to perform operation with.</param>
        /// <returns>An <see cref="Task&lt;StreamEventsSlice&gt;"/> containing the results of the read operation</returns>
        Task<StreamEventsSlice> ReadStreamEventsBackwardAsync(string stream, int start, int count, bool resolveLinkTos, UserCredentials userCredentials = null);

        /// <summary>
        /// Reads All Events in the node forward. (e.g. beginning to end)
        /// </summary>
        /// <param name="position">The position to start reading from</param>
        /// <param name="maxCount">The maximum count to read</param>
        /// <param name="resolveLinkTos">Whether to resolve LinkTo events automatically</param>
        /// <param name="userCredentials">The optional user credentials to perform operation with.</param>
        /// <returns>A <see cref="AllEventsSlice"/> containing the records read</returns>
        AllEventsSlice ReadAllEventsForward(Position position, int maxCount, bool resolveLinkTos, UserCredentials userCredentials = null);

        /// <summary>
        /// Reads All Events in the node forward asynchronously (e.g. beginning to end)
        /// </summary>
        /// <param name="position">The position to start reading from</param>
        /// <param name="maxCount">The maximum count to read</param>
        /// <param name="resolveLinkTos">Whether to resolve LinkTo events automatically</param>
        /// <param name="userCredentials">The optional user credentials to perform operation with.</param>
        /// <returns>A <see cref="AllEventsSlice"/> containing the records read</returns>
        Task<AllEventsSlice> ReadAllEventsForwardAsync(Position position, int maxCount, bool resolveLinkTos, UserCredentials userCredentials = null);

        /// <summary>
        /// Reads All Events in the node backwards (e.g. end to beginning)
        /// </summary>
        /// <param name="position">The position to start reading from</param>
        /// <param name="maxCount">The maximum count to read</param>
        /// <param name="resolveLinkTos">Whether to resolve LinkTo events automatically</param>
        /// <param name="userCredentials">The optional user credentials to perform operation with.</param>
        /// <returns>A <see cref="AllEventsSlice"/> containing the records read</returns>
        AllEventsSlice ReadAllEventsBackward(Position position, int maxCount, bool resolveLinkTos, UserCredentials userCredentials = null);

        /// <summary>
        /// Reads All Events in the node backwards (e.g. end to beginning)
        /// </summary>
        /// <param name="position">The position to start reading from</param>
        /// <param name="maxCount">The maximum count to read</param>
        /// <param name="resolveLinkTos">Whether to resolve LinkTo events automatically</param>
        /// <param name="userCredentials">The optional user credentials to perform operation with.</param>
        /// <returns>A <see cref="AllEventsSlice"/> containing the records read</returns>
        Task<AllEventsSlice> ReadAllEventsBackwardAsync(Position position, int maxCount, bool resolveLinkTos, UserCredentials userCredentials = null);

        EventStoreSubscription SubscribeToStream(
                string stream,
                bool resolveLinkTos,
                Action<EventStoreSubscription, ResolvedEvent> eventAppeared,
                Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                UserCredentials userCredentials = null);

        Task<EventStoreSubscription> SubscribeToStreamAsync(
                string stream,
                bool resolveLinkTos,
                Action<EventStoreSubscription, ResolvedEvent> eventAppeared,
                Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                UserCredentials userCredentials = null);

        EventStoreStreamCatchUpSubscription SubscribeToStreamFrom(
                string stream,
                int? fromEventNumberExclusive,
                bool resolveLinkTos,
                Action<EventStoreCatchUpSubscription, ResolvedEvent> eventAppeared,
                Action<EventStoreCatchUpSubscription> liveProcessingStarted = null,
                Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                UserCredentials userCredentials = null,
                int readBatchSize = 500);

        EventStoreSubscription SubscribeToAll(
                bool resolveLinkTos, 
                Action<EventStoreSubscription, ResolvedEvent> eventAppeared, 
                Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                UserCredentials userCredentials = null);

        Task<EventStoreSubscription> SubscribeToAllAsync(
                bool resolveLinkTos,
                Action<EventStoreSubscription, ResolvedEvent> eventAppeared,
                Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                UserCredentials userCredentials = null);

        EventStoreAllCatchUpSubscription SubscribeToAllFrom(
                Position? fromPositionExclusive,
                bool resolveLinkTos,
                Action<EventStoreCatchUpSubscription, ResolvedEvent> eventAppeared,
                Action<EventStoreCatchUpSubscription> liveProcessingStarted = null,
                Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                UserCredentials userCredentials = null,
                int readBatchSize = 500);

        WriteResult SetStreamMetadata(string stream, int expectedMetastreamVersion, StreamMetadata metadata, UserCredentials userCredentials = null);

        Task<WriteResult> SetStreamMetadataAsync(string stream, int expectedMetastreamVersion, StreamMetadata metadata, UserCredentials userCredentials = null);

        WriteResult SetStreamMetadata(string stream, int expectedMetastreamVersion, byte[] metadata, UserCredentials userCredentials = null);
        
        Task<WriteResult> SetStreamMetadataAsync(string stream, int expectedMetastreamVersion, byte[] metadata, UserCredentials userCredentials = null);
        
        StreamMetadataResult GetStreamMetadata(string stream, UserCredentials userCredentials = null);
        
        Task<StreamMetadataResult> GetStreamMetadataAsync(string stream, UserCredentials userCredentials = null);
        
        RawStreamMetadataResult GetStreamMetadataAsRawBytes(string stream, UserCredentials userCredentials = null);
        
        Task<RawStreamMetadataResult> GetStreamMetadataAsRawBytesAsync(string stream, UserCredentials userCredentials = null);

        void SetSystemSettings(SystemSettings settings, UserCredentials userCredentials = null);
        
        Task SetSystemSettingsAsync(SystemSettings settings, UserCredentials userCredentials = null);

        /// <summary>
        /// Fired when an <see cref="IEventStoreConnection"/> connects to an Event Store server.
        /// </summary>
        event EventHandler<ClientConnectionEventArgs> Connected;

        /// <summary>
        /// Fired when an <see cref="IEventStoreConnection"/> is disconnected from an Event Store server
        /// by some means other than by calling the <see cref="Close"/> method.
        /// </summary>
        event EventHandler<ClientConnectionEventArgs> Disconnected;

        /// <summary>
        /// Fired when an <see cref="IEventStoreConnection"/> is attempting to reconnect to an Event Store
        /// server following a disconnection.
        /// </summary>
        event EventHandler<ClientReconnectingEventArgs> Reconnecting;

        /// <summary>
        /// Fired when an <see cref="IEventStoreConnection"/> is closed either using the <see cref="Close"/>
        /// method, or when reconnection limits are reached without a successful connection being established.
        /// </summary>
        event EventHandler<ClientClosedEventArgs> Closed;

        /// <summary>
        /// Fired when an error is thrown on an <see cref="IEventStoreConnection"/>.
        /// </summary>
        event EventHandler<ClientErrorEventArgs> ErrorOccurred;

        /// <summary>
        /// Fired when a client fails to authenticate to an Event Store server.
        /// </summary>
        event EventHandler<ClientAuthenticationFailedEventArgs> AuthenticationFailed;
    }
}