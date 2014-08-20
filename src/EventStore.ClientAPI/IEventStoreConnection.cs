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
        /// Connects the <see cref="IEventStoreConnection"/> asynchronously to a destination
        /// </summary>
        /// <returns>A <see cref="Task"/> that can be waited upon.</returns>
        Task ConnectAsync();

        /// <summary>
        /// Closes this <see cref="IEventStoreConnection"/>
        /// </summary>
        void Close();

        /// <summary>
        /// Deletes a stream from the Event Store asynchronously
        /// </summary>
        /// <param name="stream">The name of the stream to delete.</param>
        /// <param name="expectedVersion">The expected version that the streams should have when being deleted. <see cref="ExpectedVersion"/></param>
        /// <param name="userCredentials">The optional user credentials to perform operation with.</param>
        /// <returns>A <see cref="Task"/> that can be awaited upon by the caller.</returns>
        Task<DeleteResult> DeleteStreamAsync(string stream, int expectedVersion, UserCredentials userCredentials = null);

        /// <summary>
        /// Deletes a stream from the Event Store asynchronously
        /// </summary>
        /// <param name="stream">The name of the stream to delete.</param>
        /// <param name="expectedVersion">The expected version that the streams should have when being deleted. <see cref="ExpectedVersion"/></param>
        /// <param name="hardDelete">Indicator for tombstoning vs soft-deleting the stream. Tombstoned streams can never be recreated. Soft-deleted streams
        /// can be written to again, but the EventNumber sequence will not start from 0.</param>
        /// <param name="userCredentials">The optional user credentials to perform operation with.</param>
        /// <returns>A <see cref="Task"/> that can be awaited upon by the caller.</returns>
        Task<DeleteResult> DeleteStreamAsync(string stream, int expectedVersion, bool hardDelete, UserCredentials userCredentials = null);

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
        /// Asynchronously reads a single event from a stream.
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <param name="eventNumber">The event number to read, <see cref="StreamPosition">StreamPosition.End</see> to read the last event in the stream</param>
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
        /// <returns>A <see cref="Task&lt;StreamEventsSlice&gt;"/> containing the results of the read operation</returns>
        Task<StreamEventsSlice> ReadStreamEventsForwardAsync(string stream, int start, int count, bool resolveLinkTos, UserCredentials userCredentials = null);

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
        /// <param name="resolveLinkTos">Whether to resolve Link events automatically</param>
        /// <param name="userCredentials">The optional user credentials to perform operation with.</param>
        /// <returns>A <see cref="AllEventsSlice"/> containing the records read</returns>
        Task<AllEventsSlice> ReadAllEventsBackwardAsync(Position position, int maxCount, bool resolveLinkTos, UserCredentials userCredentials = null);

        /// <summary>
        /// Asynchronously subscribes to a single event stream. New events 
        /// written to the stream while the subscription is active will be
        /// pushed to the client.
        /// </summary>
        /// <param name="stream">The stream to subscribe to</param>
        /// <param name="resolveLinkTos">Whether to resolve Link events automatically</param>
        /// <param name="eventAppeared">An action invoked when a new event is received over the subscription</param>
        /// <param name="subscriptionDropped">An action invoked if the subscription is dropped</param>
        /// <param name="userCredentials">User credentials to use for the operation</param>
        /// <returns>An <see cref="EventStoreSubscription"/> representing the subscription</returns>
        Task<EventStoreSubscription> SubscribeToStreamAsync(
                string stream,
                bool resolveLinkTos,
                Action<EventStoreSubscription, ResolvedEvent> eventAppeared,
                Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                UserCredentials userCredentials = null);

        /// <summary>
        /// Subscribes to a single event stream. Existing events from
        /// lastCheckpoint onwards are read from the stream
        /// and presented to the user of <see cref="EventStoreCatchUpSubscription"/>
        /// as if they had been pushed.
        /// 
        /// Once the end of the stream is read the subscription is
        /// transparently (to the user) switched to push new events as
        /// they are written.
        /// 
        /// The action liveProcessingStarted is called when the
        /// <see cref="EventStoreCatchUpSubscription"/> switches from the reading
        /// phase to the live subscription phase.
        /// </summary>
        /// <param name="stream">The stream to subscribe to</param>
        /// <param name="lastCheckpoint">The event number from which to start.
        /// 
        /// To receive all events in the stream, use <see cref="StreamCheckpoint.StreamStart" />.
        /// If events have already been received and resubscription from the same point
        /// is desired, use the event number of the last event processed which
        /// appeared on the subscription.
        /// 
        /// NOTE: Using <see cref="StreamPosition.Start" /> here will result in missing
        /// the first event in the stream.</param>
        /// <param name="resolveLinkTos">Whether to resolve Link events automatically</param>
        /// <param name="eventAppeared">An action invoked when an event is received over the subscription</param>
        /// <param name="liveProcessingStarted">An action invoked when the subscription switches to newly-pushed events</param>
        /// <param name="subscriptionDropped">An action invoked if the subscription is dropped</param>
        /// <param name="userCredentials">User credentials to use for the operation</param>
        /// <param name="readBatchSize">The batch size to use during the read phase</param>
        /// <returns>An <see cref="EventStoreSubscription"/> representing the subscription</returns>
        EventStoreStreamCatchUpSubscription SubscribeToStreamFrom(
                string stream,
                int? lastCheckpoint,
                bool resolveLinkTos,
                Action<EventStoreCatchUpSubscription, ResolvedEvent> eventAppeared,
                Action<EventStoreCatchUpSubscription> liveProcessingStarted = null,
                Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                UserCredentials userCredentials = null,
                int readBatchSize = 500);

        /// <summary>
        /// Asynchronously subscribes to all events in the Event Store. New
        /// events written to the stream while the subscription is active
        /// will be pushed to the client.
        /// </summary>
        /// <param name="resolveLinkTos">Whether to resolve Link events automatically</param>
        /// <param name="eventAppeared">An action invoked when a new event is received over the subscription</param>
        /// <param name="subscriptionDropped">An action invoked if the subscription is dropped</param>
        /// <param name="userCredentials">User credentials to use for the operation</param>
        /// <returns>An <see cref="EventStoreSubscription"/> representing the subscription</returns>
        Task<EventStoreSubscription> SubscribeToAllAsync(
                bool resolveLinkTos,
                Action<EventStoreSubscription, ResolvedEvent> eventAppeared,
                Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                UserCredentials userCredentials = null);

        /// <summary>
        /// Subscribes a persistent subscription (competing consumer) to the event store
        /// </summary>
        /// <param name="groupName">The subscription group to connect to</param>
        /// <param name="stream">The stream to subscribe to</param>
        /// <param name="eventAppeared">An action invoked when an event appears</param>
        /// <param name="subscriptionDropped">An action invoked if the subscription is dropped</param>
        /// <param name="userCredentials">User credentials to use for the operation</param>
        /// <param name="bufferSize">The buffer size to use for the persistent subscription</param>
        /// <param name="autoAck">Whether the subscription should automatically acknowledge messages processed.
        /// If not set the receiver is required to explicitly acknowledge messages through the subscription.</param>
        /// <remarks>This will connect you to a persistent subscription group for a stream. The subscription group
        /// must first be created with CreatePersistentSubscriptionGroup many connections
        /// can connect to the same group and they will be treated as competing consumers within the group.
        /// If one connection dies work will be balanced across the rest of the consumers in the group. If
        /// you attempt to connect to a group that does not exist you will be given an exception.
        /// </remarks>
        /// <returns>An <see cref="EventStoreSubscription"/> representing the subscription</returns>
        EventStorePersistentSubscription ConnectToPersistentSubscription(
            string groupName, 
            string stream, 
            Action<EventStorePersistentSubscription, ResolvedEvent> eventAppeared,
            Action<EventStorePersistentSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
            UserCredentials userCredentials = null,
            int? bufferSize = null,
            bool autoAck = true);

        /// <summary>
        /// Subscribes a persistent subscription (competing consumer) to all events in the event store
        /// </summary>
        /// <param name="groupName">The subscription group to connect to</param>
        /// <param name="eventAppeared">An action invoked when an event appears</param>
        /// <param name="subscriptionDropped">An action invoked if the subscription is dropped</param>
        /// <param name="userCredentials">User credentials to use for the operation</param>
        /// <param name="bufferSize">The buffer size to use for the persistent subscription</param>
        /// <param name="autoAck">Whether the subscription should automatically acknowledge messages processed.
        /// If not set the receiver is required to explicitly acknowledge messages through the subscription.</param>
        /// <remarks>This will connect you to a persistent subscription group for all events. The subscription group
        /// must first be created with CreatePersistentSubscriptionGroup many connections
        /// can connect to the same group and they will be treated as competing consumers within the group.
        /// If one connection dies work will be balanced across the rest of the consumers in the group. If
        /// you attempt to connect to a group that does not exist you will be given an exception.
        /// </remarks>
        /// <returns>An <see cref="EventStoreSubscription"/> representing the subscription</returns>
        EventStorePersistentSubscription ConnectToPersistentSubscriptionForAll(
            string groupName,
            Action<EventStorePersistentSubscription, ResolvedEvent> eventAppeared,
            Action<EventStorePersistentSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
            UserCredentials userCredentials = null,
            int? bufferSize = null,
            bool autoAck = true);
        

        /// <summary>
        /// Subscribes to a all events. Existing events from lastCheckpoint
        /// onwards are read from the Event Store and presented to the user of
        /// <see cref="EventStoreCatchUpSubscription"/> as if they had been pushed.
        /// 
        /// Once the end of the stream is read the subscription is
        /// transparently (to the user) switched to push new events as
        /// they are written.
        /// 
        /// The action liveProcessingStarted is called when the
        /// <see cref="EventStoreCatchUpSubscription"/> switches from the reading
        /// phase to the live subscription phase.
        /// </summary>
        /// <param name="lastCheckpoint">The position from which to start.
        /// 
        /// To receive all events in the database, use <see cref="AllCheckpoint.AllStart" />.
        /// If events have already been received and resubscription from the same point
        /// is desired, use the position representing the last event processed which
        /// appeared on the subscription.
        /// 
        /// NOTE: Using <see cref="Position.Start" /> here will result in missing
        /// the first event in the stream.</param>
        /// <param name="resolveLinkTos">Whether to resolve Link events automatically</param>
        /// <param name="eventAppeared">An action invoked when an event is received over the subscription</param>
        /// <param name="liveProcessingStarted">An action invoked when the subscription switches to newly-pushed events</param>
        /// <param name="subscriptionDropped">An action invoked if the subscription is dropped</param>
        /// <param name="userCredentials">User credentials to use for the operation</param>
        /// <param name="readBatchSize">The batch size to use during the read phase</param>
        /// <returns>An <see cref="EventStoreSubscription"/> representing the subscription</returns>
        EventStoreAllCatchUpSubscription SubscribeToAllFrom(
                Position? lastCheckpoint,
                bool resolveLinkTos,
                Action<EventStoreCatchUpSubscription, ResolvedEvent> eventAppeared,
                Action<EventStoreCatchUpSubscription> liveProcessingStarted = null,
                Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                UserCredentials userCredentials = null,
                int readBatchSize = 500);

        /// <summary>
        /// Asynchronously create a persistent subscription group for all events
        /// </summary>
        /// <param name="groupName">The name of the group to create</param>
        /// <param name="resolveLinkTos">Whether or not to resolved link tos on this subscription group</param>
        /// <param name="userCredentials">User credentials to use for the operation</param>
        /// <returns>A <see cref="PersistentSubscriptionCreateResult"/>.</returns>
        Task<PersistentSubscriptionCreateResult> CreatePersistentSubscriptionForAllAsync(string groupName, bool resolveLinkTos, UserCredentials userCredentials = null);

        /// <summary>
        /// Asynchronously create a persistent subscription group on a stream
        /// </summary>
        /// <param name="stream">The name of the stream to create the persistent subscription on</param>
        /// <param name="groupName">The name of the group to create</param>
        /// <param name="resolveLinkTos">Whether or not to resolved link tos on this subscription group</param>
        /// <param name="userCredentials">User credentials to use for the operation</param>
        /// <returns>A <see cref="PersistentSubscriptionCreateResult"/>.</returns>
        Task<PersistentSubscriptionCreateResult> CreatePersistentSubscriptionAsync(string stream, string groupName, bool resolveLinkTos, UserCredentials userCredentials = null);


        /// <summary>
        /// Asynchronously delete a persistent subscription group on a stream
        /// </summary>
        /// <param name="stream">The name of the stream to delete the persistent subscription on</param>
        /// <param name="groupName">The name of the group to delete</param>
        /// <param name="userCredentials">User credentials to use for the operation</param>
        /// <returns>A <see cref="PersistentSubscriptionDeleteResult"/>.</returns>
        Task<PersistentSubscriptionDeleteResult> DeletePersistentSubscriptionAsync(string stream, string groupName, UserCredentials userCredentials = null);

        /// <summary>
        /// Asynchronously delete a persistent subscription group for all events
        /// </summary>
        /// <param name="groupName">The name of the group to delete</param>
        /// <param name="userCredentials">User credentials to use for the operation</param>
        /// <returns>A <see cref="PersistentSubscriptionDeleteResult"/>.</returns>
        Task<PersistentSubscriptionDeleteResult> DeletePersistentSubscriptionForAllAsync(string groupName, UserCredentials userCredentials = null);


        /// <summary>
        /// Asynchronously sets the metadata for a stream.
        /// </summary>
        /// <param name="stream">The name of the stream for which to set metadata.</param>
        /// <param name="expectedMetastreamVersion">The expected version for the write to the metadata stream.</param>
        /// <param name="metadata">A <see cref="StreamMetadata"/> representing the new metadata.</param>
        /// <param name="userCredentials">User credentials to use for the operation</param>
        /// <returns>A <see cref="WriteResult"/>.</returns>
        Task<WriteResult> SetStreamMetadataAsync(string stream, int expectedMetastreamVersion, StreamMetadata metadata, UserCredentials userCredentials = null);

        /// <summary>
        /// Asynchronously sets the metadata for a stream.
        /// </summary>
        /// <param name="stream">The name of the stream for which to set metadata.</param>
        /// <param name="expectedMetastreamVersion">The expected version for the write to the metadata stream.</param>
        /// <param name="metadata">A byte array representing the new metadata.</param>
        /// <param name="userCredentials">User credentials to use for the operation.</param>
        /// <returns>A <see cref="WriteResult"/>.</returns>
        Task<WriteResult> SetStreamMetadataAsync(string stream, int expectedMetastreamVersion, byte[] metadata, UserCredentials userCredentials = null);
        
        /// <summary>
        /// Asynchronously reads the metadata for a stream and converts the metadata into a <see cref="StreamMetadata"/>.
        /// </summary>
        /// <param name="stream">The name of the stream for which to read metadata.</param>
        /// <param name="userCredentials">User credentials to use for the operation.</param>
        /// <returns>A <see cref="StreamMetadataResult"/> representing the result of the operation.</returns>
        Task<StreamMetadataResult> GetStreamMetadataAsync(string stream, UserCredentials userCredentials = null);

        /// <summary>
        /// Asynchronously reads the metadata for a stream as a byte array.
        /// </summary>
        /// <param name="stream">The name of the stream for which to read metadata.</param>
        /// <param name="userCredentials">User credentials to use for the operation.</param>
        /// <returns>A <see cref="StreamMetadataResult"/> representing the result of the operation.</returns>
        Task<RawStreamMetadataResult> GetStreamMetadataAsRawBytesAsync(string stream, UserCredentials userCredentials = null);

        /// <summary>
        /// Sets the global settings for the server or cluster to which the <see cref="IEventStoreConnection"/>
        /// is connected.
        /// </summary>
        /// <param name="settings">The <see cref="SystemSettings"/> to apply.</param>
        /// <param name="userCredentials">User credentials to use for the operation.</param>
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