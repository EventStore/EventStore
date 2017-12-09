using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI.ClientOperations;
using EventStore.ClientAPI.Common;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI.Internal
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
        public string ConnectionName { get; }
        private readonly IEndPointDiscoverer _endPointDiscoverer;
        private readonly EventStoreConnectionLogicHandler _handler;

        /// <summary>
        /// Returns the <see cref="ConnectionSettings"/> use to create this connection
        /// </summary>
        public ConnectionSettings Settings { get; }

        /// <summary>
        /// Returns the <see cref="ClusterSettings"/> use to create this connection
        /// </summary>
        public ClusterSettings ClusterSettings { get; }

        /// <summary>
        /// Constructs a new instance of a <see cref="EventStoreConnection"/>
        /// </summary>
        /// <param name="settings">The <see cref="ConnectionSettings"/> containing the settings for this connection.</param>
        /// <param name="clusterSettings">The <see cref="ClusterSettings" /> containing the settings for this connection.</param>
        /// <param name="endPointDiscoverer">Discoverer of destination node end point.</param>
        /// <param name="connectionName">Optional name of connection (will be generated automatically, if not provided)</param>
        internal EventStoreNodeConnection(ConnectionSettings settings, ClusterSettings clusterSettings, IEndPointDiscoverer endPointDiscoverer, string connectionName)
        {
            Ensure.NotNull(settings, "settings");
            Ensure.NotNull(endPointDiscoverer, "endPointDiscoverer");

            ConnectionName = connectionName ?? string.Format("ES-{0}", Guid.NewGuid());
            Settings = settings;
            ClusterSettings = clusterSettings;
            _endPointDiscoverer = endPointDiscoverer;
            _handler = new EventStoreConnectionLogicHandler(this, settings);
        }

        public Task ConnectAsync()
        {
            var source = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            _handler.EnqueueMessage(new StartConnectionMessage(source, _endPointDiscoverer));
            return source.Task;
        }

        void IDisposable.Dispose()
        {
            Close();
        }

        public void Close()
        {
            _handler.EnqueueMessage(new CloseConnectionMessage("Connection close requested by client.", null));
        }

        public Task<DeleteResult> DeleteStreamAsync(string stream, long expectedVersion, UserCredentials userCredentials = null)
        {
            return DeleteStreamAsync(stream, expectedVersion, false, userCredentials);
        }

        public Task<DeleteResult> DeleteStreamAsync(string stream, long expectedVersion, bool hardDelete, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(stream, "stream");

            var source = new TaskCompletionSource<DeleteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            EnqueueOperation(new DeleteStreamOperation(Settings.Log, source, Settings.RequireMaster,
                                                       stream, expectedVersion, hardDelete, userCredentials));
            return source.Task;
        }

        public Task<WriteResult> AppendToStreamAsync(string stream, long expectedVersion, params EventData[] events)
        {
// ReSharper disable RedundantArgumentDefaultValue
// ReSharper disable RedundantCast
            return AppendToStreamAsync(stream, expectedVersion, (IEnumerable<EventData>) events, null);
// ReSharper restore RedundantCast
// ReSharper restore RedundantArgumentDefaultValue
        }

        public Task<WriteResult> AppendToStreamAsync(string stream, long expectedVersion, UserCredentials userCredentials, params EventData[] events)
        {
// ReSharper disable RedundantCast
            return AppendToStreamAsync(stream, expectedVersion, (IEnumerable<EventData>)events, userCredentials);
// ReSharper restore RedundantCast
        }

        public Task<WriteResult> AppendToStreamAsync(string stream, long expectedVersion, IEnumerable<EventData> events, UserCredentials userCredentials = null)
        {
// ReSharper disable PossibleMultipleEnumeration
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.NotNull(events, "events");

            var source = new TaskCompletionSource<WriteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            EnqueueOperation(new AppendToStreamOperation(Settings.Log, source, Settings.RequireMaster,
                                                         stream, expectedVersion, events, userCredentials));
            return source.Task;
// ReSharper restore PossibleMultipleEnumeration
        }

        public Task<ConditionalWriteResult> ConditionalAppendToStreamAsync(string stream, long expectedVersion, IEnumerable<EventData> events,
            UserCredentials userCredentials = null)
        {
            // ReSharper disable PossibleMultipleEnumeration
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.NotNull(events, "events");

            var source = new TaskCompletionSource<ConditionalWriteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            EnqueueOperation(new ConditionalAppendToStreamOperation(Settings.Log, source, Settings.RequireMaster,
                                                         stream, expectedVersion, events, userCredentials));
            return source.Task;
            // ReSharper restore PossibleMultipleEnumeration
        }

        public Task<EventStoreTransaction> StartTransactionAsync(string stream, long expectedVersion, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(stream, "stream");

            var source = new TaskCompletionSource<EventStoreTransaction>(TaskCreationOptions.RunContinuationsAsynchronously);
            EnqueueOperation(new StartTransactionOperation(Settings.Log, source, Settings.RequireMaster,
                                                           stream, expectedVersion, this, userCredentials));
            return source.Task;
        }

        public EventStoreTransaction ContinueTransaction(long transactionId, UserCredentials userCredentials = null)
        {
            Ensure.Nonnegative(transactionId, "transactionId");
            return new EventStoreTransaction(transactionId, userCredentials, this);
        }

        Task IEventStoreTransactionConnection.TransactionalWriteAsync(EventStoreTransaction transaction, IEnumerable<EventData> events, UserCredentials userCredentials)
        {
// ReSharper disable PossibleMultipleEnumeration
            Ensure.NotNull(transaction, "transaction");
            Ensure.NotNull(events, "events");

            var source = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            EnqueueOperation(new TransactionalWriteOperation(Settings.Log, source, Settings.RequireMaster,
                                                             transaction.TransactionId, events, userCredentials));
            return source.Task;
// ReSharper restore PossibleMultipleEnumeration
        }

        Task<WriteResult> IEventStoreTransactionConnection.CommitTransactionAsync(EventStoreTransaction transaction, UserCredentials userCredentials)
        {
            Ensure.NotNull(transaction, "transaction");

            var source = new TaskCompletionSource<WriteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            EnqueueOperation(new CommitTransactionOperation(Settings.Log, source, Settings.RequireMaster,
                                                            transaction.TransactionId, userCredentials));
            return source.Task;
        }


        public Task<EventReadResult> ReadEventAsync(string stream, long eventNumber, bool resolveLinkTos, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            if (eventNumber < -1) throw new ArgumentOutOfRangeException(nameof(eventNumber));
            var source = new TaskCompletionSource<EventReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            var operation = new ReadEventOperation(Settings.Log, source, stream, eventNumber, resolveLinkTos,
                                                   Settings.RequireMaster, userCredentials);
            EnqueueOperation(operation);
            return source.Task;
        }

        public Task<StreamEventsSlice> ReadStreamEventsForwardAsync(string stream, long start, int count, bool resolveLinkTos, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.Nonnegative(start, "start");
            Ensure.Positive(count, "count");
            if(count > ClientApiConstants.MaxReadSize) throw new ArgumentException(string.Format("Count should be less than {0}. For larger reads you should page.", ClientApiConstants.MaxReadSize));
            var source = new TaskCompletionSource<StreamEventsSlice>(TaskCreationOptions.RunContinuationsAsynchronously);
            var operation = new ReadStreamEventsForwardOperation(Settings.Log, source, stream, start, count,
                                                                 resolveLinkTos, Settings.RequireMaster, userCredentials);
            EnqueueOperation(operation);
            return source.Task;
        }

        public Task<StreamEventsSlice> ReadStreamEventsBackwardAsync(string stream, long start, int count, bool resolveLinkTos, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.Positive(count, "count");
            if (count > ClientApiConstants.MaxReadSize) throw new ArgumentException(string.Format("Count should be less than {0}. For larger reads you should page.", ClientApiConstants.MaxReadSize));
            var source = new TaskCompletionSource<StreamEventsSlice>(TaskCreationOptions.RunContinuationsAsynchronously);
            var operation = new ReadStreamEventsBackwardOperation(Settings.Log, source, stream, start, count,
                                                                  resolveLinkTos, Settings.RequireMaster, userCredentials);
            EnqueueOperation(operation);
            return source.Task;
        }

        public Task<AllEventsSlice> ReadAllEventsForwardAsync(Position position, int maxCount, bool resolveLinkTos, UserCredentials userCredentials = null)
        {
            Ensure.Positive(maxCount, "maxCount");
            if (maxCount > ClientApiConstants.MaxReadSize) throw new ArgumentException(string.Format("Count should be less than {0}. For larger reads you should page.", ClientApiConstants.MaxReadSize));
            var source = new TaskCompletionSource<AllEventsSlice>(TaskCreationOptions.RunContinuationsAsynchronously);
            var operation = new ReadAllEventsForwardOperation(Settings.Log, source, position, maxCount,
                                                              resolveLinkTos, Settings.RequireMaster, userCredentials);
            EnqueueOperation(operation);
            return source.Task;
        }

        public Task<AllEventsSlice> ReadAllEventsBackwardAsync(Position position, int maxCount, bool resolveLinkTos, UserCredentials userCredentials = null)
        {
            Ensure.Positive(maxCount, "maxCount");
            if (maxCount > ClientApiConstants.MaxReadSize) throw new ArgumentException(string.Format("Count should be less than {0}. For larger reads you should page.", ClientApiConstants.MaxReadSize));
            var source = new TaskCompletionSource<AllEventsSlice>(TaskCreationOptions.RunContinuationsAsynchronously);
            var operation = new ReadAllEventsBackwardOperation(Settings.Log, source, position, maxCount,
                                                               resolveLinkTos, Settings.RequireMaster, userCredentials);
            EnqueueOperation(operation);
            return source.Task;
        }

        private void EnqueueOperation(IClientOperation operation)
        {
            while (_handler.TotalOperationCount >= Settings.MaxQueueSize)
            {
                Thread.Sleep(1);
            }
            _handler.EnqueueMessage(new StartOperationMessage(operation, Settings.MaxRetries, Settings.OperationTimeout));
        }

        public Task<EventStoreSubscription> SubscribeToStreamAsync(
                string stream,
                bool resolveLinkTos,
                Func<EventStoreSubscription, ResolvedEvent, Task> eventAppeared,
                Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.NotNull(eventAppeared, "eventAppeared");

            var source = new TaskCompletionSource<EventStoreSubscription>(TaskCreationOptions.RunContinuationsAsynchronously);
            _handler.EnqueueMessage(new StartSubscriptionMessage(source, stream, resolveLinkTos, userCredentials,
                                                                 eventAppeared, subscriptionDropped,
                                                                 Settings.MaxRetries, Settings.OperationTimeout));
            return source.Task;
        }

        public EventStoreStreamCatchUpSubscription SubscribeToStreamFrom(string stream,
                                                                         long? lastCheckpoint,
                                                                         bool resolveLinkTos,
                                                                         Func<EventStoreCatchUpSubscription, ResolvedEvent, Task> eventAppeared,
                                                                         Action<EventStoreCatchUpSubscription> liveProcessingStarted = null,
                                                                         Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                                                                         UserCredentials userCredentials = null,
                                                                         int readBatchSize = 500,
                                                                         string subscriptionName = "")
        {
            var settings = new CatchUpSubscriptionSettings(Consts.CatchUpDefaultMaxPushQueueSize, readBatchSize,
                                                                                    Settings.VerboseLogging,
                                                                                    resolveLinkTos,
                                                                                    subscriptionName);
            return SubscribeToStreamFrom(stream, lastCheckpoint, settings, eventAppeared, liveProcessingStarted, subscriptionDropped, userCredentials);
        }

        public EventStoreStreamCatchUpSubscription SubscribeToStreamFrom(
                string stream,
                long? lastCheckpoint,
                CatchUpSubscriptionSettings settings,
                Func<EventStoreCatchUpSubscription, ResolvedEvent, Task> eventAppeared,
                Action<EventStoreCatchUpSubscription> liveProcessingStarted = null,
                Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.NotNull(settings, "settings");
            Ensure.NotNull(eventAppeared, "eventAppeared");
            var catchUpSubscription =
                    new EventStoreStreamCatchUpSubscription(this, Settings.Log, stream, lastCheckpoint,
                                                            userCredentials, eventAppeared, liveProcessingStarted,
                                                            subscriptionDropped, settings);
            catchUpSubscription.StartAsync();
            return catchUpSubscription;
        }

        public Task<EventStoreSubscription> SubscribeToAllAsync(
                bool resolveLinkTos,
                Func<EventStoreSubscription, ResolvedEvent, Task> eventAppeared,
                Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                UserCredentials userCredentials = null)
        {
            Ensure.NotNull(eventAppeared, "eventAppeared");

            var source = new TaskCompletionSource<EventStoreSubscription>(TaskCreationOptions.RunContinuationsAsynchronously);
            _handler.EnqueueMessage(new StartSubscriptionMessage(source, string.Empty, resolveLinkTos, userCredentials,
                                                                 eventAppeared, subscriptionDropped,
                                                                 Settings.MaxRetries, Settings.OperationTimeout));
            return source.Task;
        }

        public EventStoreAllCatchUpSubscription SubscribeToAllFrom(
            Position? lastCheckpoint,
            bool resolveLinkTos,
            Func<EventStoreCatchUpSubscription, ResolvedEvent, Task> eventAppeared,
            Action<EventStoreCatchUpSubscription> liveProcessingStarted = null,
            Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
            UserCredentials userCredentials = null,
            int readBatchSize = 500,
            string subscriptionName = "")
        {
            var settings = new CatchUpSubscriptionSettings(Consts.CatchUpDefaultMaxPushQueueSize, readBatchSize,
                                                                                    Settings.VerboseLogging, resolveLinkTos, subscriptionName);
            return SubscribeToAllFrom(lastCheckpoint, settings,eventAppeared, liveProcessingStarted, subscriptionDropped, userCredentials);
        }

        public EventStoreAllCatchUpSubscription SubscribeToAllFrom(
            Position? lastCheckpoint,
            CatchUpSubscriptionSettings settings,
            Func<EventStoreCatchUpSubscription, ResolvedEvent, Task> eventAppeared,
            Action<EventStoreCatchUpSubscription> liveProcessingStarted = null,
            Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
            UserCredentials userCredentials = null)
        {
            Ensure.NotNull(eventAppeared, "eventAppeared");
            Ensure.NotNull(settings, "settings");
            var catchUpSubscription =
                    new EventStoreAllCatchUpSubscription(this, Settings.Log, lastCheckpoint,
                                                         userCredentials, eventAppeared, liveProcessingStarted,
                                                         subscriptionDropped, settings);
            catchUpSubscription.StartAsync();
            return catchUpSubscription;
        }

        public EventStorePersistentSubscriptionBase ConnectToPersistentSubscription(
            string stream,
            string groupName,
            Func<EventStorePersistentSubscriptionBase, ResolvedEvent, int?, Task> eventAppeared,
            Action<EventStorePersistentSubscriptionBase, SubscriptionDropReason, Exception> subscriptionDropped = null,
            UserCredentials userCredentials = null,
            int bufferSize = 10,
            bool autoAck = true)
        {
            Ensure.NotNullOrEmpty(groupName, "groupName");
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.NotNull(eventAppeared, "eventAppeared");

            var subscription = new EventStorePersistentSubscription(
                groupName, stream, eventAppeared, subscriptionDropped, userCredentials, Settings.Log,
                Settings.VerboseLogging, Settings, _handler, bufferSize, autoAck);

            subscription.Start().Wait();
            return subscription;
        }

        public Task<EventStorePersistentSubscriptionBase> ConnectToPersistentSubscriptionAsync(
            string stream,
            string groupName,
            Func<EventStorePersistentSubscriptionBase, ResolvedEvent, int?, Task> eventAppeared,
            Action<EventStorePersistentSubscriptionBase, SubscriptionDropReason, Exception> subscriptionDropped = null,
            UserCredentials userCredentials = null,
            int bufferSize = 10,
            bool autoAck = true)
        {
            Ensure.NotNullOrEmpty(groupName, "groupName");
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.NotNull(eventAppeared, "eventAppeared");

            var subscription = new EventStorePersistentSubscription(
                groupName, stream, eventAppeared, subscriptionDropped, userCredentials, Settings.Log,
                Settings.VerboseLogging, Settings, _handler, bufferSize, autoAck);

            return subscription.Start();
        }
        /*

                public EventStorePersistentSubscription ConnectToPersistentSubscriptionForAll(
                    string groupName,
                    Action<EventStorePersistentSubscription, ResolvedEvent> eventAppeared,
                    Action<EventStorePersistentSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                    UserCredentials userCredentials = null,
                    int? bufferSize = null,
                    bool autoAck = true)
                {
                    return ConnectToPersistentSubscription(groupName,
                        SystemStreams.AllStream,
                        eventAppeared,
                        subscriptionDropped,
                        userCredentials,
                        bufferSize,
                        autoAck);
                }
        */


        public Task CreatePersistentSubscriptionAsync(string stream, string groupName, PersistentSubscriptionSettings settings, UserCredentials credentials = null) {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.NotNullOrEmpty(groupName, "groupName");
            Ensure.NotNull(settings, "settings");
            var source = new TaskCompletionSource<PersistentSubscriptionCreateResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            EnqueueOperation(new CreatePersistentSubscriptionOperation(Settings.Log, source, stream, groupName, settings, credentials));
            return source.Task;
        }

        public Task UpdatePersistentSubscriptionAsync(string stream, string groupName, PersistentSubscriptionSettings settings, UserCredentials credentials = null)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.NotNullOrEmpty(groupName, "groupName");
            Ensure.NotNull(settings, "settings");
            var source = new TaskCompletionSource<PersistentSubscriptionUpdateResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            EnqueueOperation(new UpdatePersistentSubscriptionOperation(Settings.Log, source, stream, groupName, settings, credentials));
            return source.Task;
        }
/*

        public Task<PersistentSubscriptionCreateResult> CreatePersistentSubscriptionForAllAsync(string groupName, PersistentSubscriptionSettings settings, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(groupName, "groupName");
            Ensure.NotNull(settings, "settings");
            var source = new TaskCompletionSource<PersistentSubscriptionCreateResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            EnqueueOperation(new CreatePersistentSubscriptionOperation(_settings.Log, source, SystemStreams.AllStream, groupName, settings, userCredentials));
            return source.Task;
        }

*/
        public Task DeletePersistentSubscriptionAsync(string stream, string groupName, UserCredentials userCredentials = null) {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.NotNullOrEmpty(groupName, "groupName");
            var source = new TaskCompletionSource<PersistentSubscriptionDeleteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            EnqueueOperation(new DeletePersistentSubscriptionOperation(Settings.Log, source, stream, groupName, userCredentials));
            return source.Task;
        }
/*

        public Task<PersistentSubscriptionDeleteResult> DeletePersistentSubscriptionForAllAsync(string groupName, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(groupName, "groupName");
            var source = new TaskCompletionSource<PersistentSubscriptionDeleteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            EnqueueOperation(new DeletePersistentSubscriptionOperation(_settings.Log, source, SystemStreams.AllStream, groupName, userCredentials));
            return source.Task;
        }

*/

        public Task<WriteResult> SetStreamMetadataAsync(string stream, long expectedMetastreamVersion, StreamMetadata metadata, UserCredentials userCredentials = null)
        {
            return SetStreamMetadataAsync(stream, expectedMetastreamVersion, metadata.AsJsonBytes(), userCredentials);
        }

        public Task<WriteResult> SetStreamMetadataAsync(string stream, long expectedMetastreamVersion, byte[] metadata, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            if (SystemStreams.IsMetastream(stream))
                throw new ArgumentException(string.Format("Setting metadata for metastream '{0}' is not supported.", stream), nameof(stream));

            var source = new TaskCompletionSource<WriteResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            var metaevent = new EventData(Guid.NewGuid(), SystemEventTypes.StreamMetadata, true, metadata ?? Empty.ByteArray, null);
            EnqueueOperation(new AppendToStreamOperation(Settings.Log,
                                                         source,
                                                         Settings.RequireMaster,
                                                         SystemStreams.MetastreamOf(stream),
                                                         expectedMetastreamVersion,
                                                         new[] { metaevent },
                                                         userCredentials));
            return source.Task;
        }

        public Task<StreamMetadataResult> GetStreamMetadataAsync(string stream, UserCredentials userCredentials = null)
        {
            return GetStreamMetadataAsRawBytesAsync(stream, userCredentials).ContinueWith(t =>
            {
                if (t.Exception != null)
                    throw t.Exception.InnerException;
                var res = t.Result;
                if (res.StreamMetadata == null || res.StreamMetadata.Length == 0)
                    return new StreamMetadataResult(res.Stream, res.IsStreamDeleted, res.MetastreamVersion, StreamMetadata.Create());
                var metadata = StreamMetadata.FromJsonBytes(res.StreamMetadata);
                return new StreamMetadataResult(res.Stream, res.IsStreamDeleted, res.MetastreamVersion, metadata);
            });
        }

        public Task<RawStreamMetadataResult> GetStreamMetadataAsRawBytesAsync(string stream, UserCredentials userCredentials = null)
        {
            return ReadEventAsync(SystemStreams.MetastreamOf(stream), -1, false, userCredentials).ContinueWith(t =>
            {
                if (t.Exception != null)
                    throw t.Exception.InnerException;

                var res = t.Result;
                switch (res.Status)
                {
                    case EventReadStatus.Success:
                        if (res.Event == null) throw new Exception("Event is null while operation result is Success.");
                        var evnt = res.Event.Value.OriginalEvent;
                        if (evnt == null) return new RawStreamMetadataResult(stream, false, -1, Empty.ByteArray);
                        return new RawStreamMetadataResult(stream, false, evnt.EventNumber, evnt.Data);
                    case EventReadStatus.NotFound:
                    case EventReadStatus.NoStream:
                        return new RawStreamMetadataResult(stream, false, -1, Empty.ByteArray);
                    case EventReadStatus.StreamDeleted:
                        return new RawStreamMetadataResult(stream, true, long.MaxValue, Empty.ByteArray);
                    default:
                        throw new ArgumentOutOfRangeException(string.Format("Unexpected ReadEventResult: {0}.", res.Status));
                }
            });
        }

        public Task SetSystemSettingsAsync(SystemSettings settings, UserCredentials userCredentials = null)
        {
            return AppendToStreamAsync(SystemStreams.SettingsStream, ExpectedVersion.Any, userCredentials,
                                       new EventData(Guid.NewGuid(), SystemEventTypes.Settings, true, settings.ToJsonBytes(), null));
        }

        public event EventHandler<ClientConnectionEventArgs> Connected
        {
            add
            {
                _handler.Connected += value;
            }
            remove
            {
                _handler.Connected -= value;
            }
        }

        public event EventHandler<ClientConnectionEventArgs> Disconnected
        {
            add
            {
                _handler.Disconnected += value;
            }
            remove
            {
                _handler.Disconnected -= value;
            }
        }

        public event EventHandler<ClientReconnectingEventArgs> Reconnecting
        {
            add
            {
                _handler.Reconnecting += value;
            }
            remove
            {
                _handler.Reconnecting -= value;
            }
        }

        public event EventHandler<ClientClosedEventArgs> Closed
        {
            add
            {
                _handler.Closed += value;
            }
            remove
            {
                _handler.Closed -= value;
            }
        }

        public event EventHandler<ClientErrorEventArgs> ErrorOccurred
        {
            add
            {
                _handler.ErrorOccurred += value;
            }
            remove
            {
                _handler.ErrorOccurred -= value;
            }
        }

        public event EventHandler<ClientAuthenticationFailedEventArgs> AuthenticationFailed
        {
            add
            {
                _handler.AuthenticationFailed += value;
            }
            remove
            {
                _handler.AuthenticationFailed -= value;
            }
        }
    }
}