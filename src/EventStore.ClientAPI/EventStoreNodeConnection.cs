using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI.ClientOperations;
using EventStore.ClientAPI.Common;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Core;
using EventStore.ClientAPI.SystemData;

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

        public Task ConnectAsync()
        {
            var source = new TaskCompletionSource<object>();
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

        public Task<DeleteResult> DeleteStreamAsync(string stream, int expectedVersion, UserCredentials userCredentials = null)
        {
            return DeleteStreamAsync(stream, expectedVersion, false, userCredentials);
        }

        public Task<DeleteResult> DeleteStreamAsync(string stream, int expectedVersion, bool hardDelete, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(stream, "stream");

            var source = new TaskCompletionSource<DeleteResult>();
            EnqueueOperation(new DeleteStreamOperation(_settings.Log, source, _settings.RequireMaster, 
                                                       stream, expectedVersion, hardDelete, userCredentials));
            return source.Task;
        }

        public Task<WriteResult> AppendToStreamAsync(string stream, int expectedVersion, params EventData[] events)
        {
// ReSharper disable RedundantArgumentDefaultValue
// ReSharper disable RedundantCast
            return AppendToStreamAsync(stream, expectedVersion, (IEnumerable<EventData>) events, null);
// ReSharper restore RedundantCast
// ReSharper restore RedundantArgumentDefaultValue
        }

        public Task<WriteResult> AppendToStreamAsync(string stream, int expectedVersion, UserCredentials userCredentials, params EventData[] events)
        {
// ReSharper disable RedundantCast
            return AppendToStreamAsync(stream, expectedVersion, (IEnumerable<EventData>)events, userCredentials);
// ReSharper restore RedundantCast
        }

        public Task<WriteResult> AppendToStreamAsync(string stream, int expectedVersion, IEnumerable<EventData> events, UserCredentials userCredentials = null)
        {
// ReSharper disable PossibleMultipleEnumeration
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.NotNull(events, "events");

            var source = new TaskCompletionSource<WriteResult>();
            EnqueueOperation(new AppendToStreamOperation(_settings.Log, source, _settings.RequireMaster, 
                                                         stream, expectedVersion, events, userCredentials));
            return source.Task;
// ReSharper restore PossibleMultipleEnumeration
        }

        public Task<EventStoreTransaction> StartTransactionAsync(string stream, int expectedVersion, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(stream, "stream");

            var source = new TaskCompletionSource<EventStoreTransaction>();
            EnqueueOperation(new StartTransactionOperation(_settings.Log, source, _settings.RequireMaster, 
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

            var source = new TaskCompletionSource<object>();
            EnqueueOperation(new TransactionalWriteOperation(_settings.Log, source, _settings.RequireMaster, 
                                                             transaction.TransactionId, events, userCredentials));
            return source.Task;
// ReSharper restore PossibleMultipleEnumeration
        }

        Task<WriteResult> IEventStoreTransactionConnection.CommitTransactionAsync(EventStoreTransaction transaction, UserCredentials userCredentials)
        {
            Ensure.NotNull(transaction, "transaction");

            var source = new TaskCompletionSource<WriteResult>();
            EnqueueOperation(new CommitTransactionOperation(_settings.Log, source, _settings.RequireMaster, 
                                                            transaction.TransactionId, userCredentials));
            return source.Task;
        }


        public Task<EventReadResult> ReadEventAsync(string stream, int eventNumber, bool resolveLinkTos, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            if (eventNumber < -1) throw new ArgumentOutOfRangeException("eventNumber");
            var source = new TaskCompletionSource<EventReadResult>();
            var operation = new ReadEventOperation(_settings.Log, source, stream, eventNumber, resolveLinkTos,
                                                   _settings.RequireMaster, userCredentials);
            EnqueueOperation(operation);
            return source.Task;
        }

        public Task<StreamEventsSlice> ReadStreamEventsForwardAsync(string stream, int start, int count, bool resolveLinkTos, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.Nonnegative(start, "start");
            Ensure.Positive(count, "count");

            var source = new TaskCompletionSource<StreamEventsSlice>();
            var operation = new ReadStreamEventsForwardOperation(_settings.Log, source, stream, start, count,
                                                                 resolveLinkTos, _settings.RequireMaster, userCredentials);
            EnqueueOperation(operation);
            return source.Task;
        }

        public Task<StreamEventsSlice> ReadStreamEventsBackwardAsync(string stream, int start, int count, bool resolveLinkTos, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.Positive(count, "count");

            var source = new TaskCompletionSource<StreamEventsSlice>();
            var operation = new ReadStreamEventsBackwardOperation(_settings.Log, source, stream, start, count,
                                                                  resolveLinkTos, _settings.RequireMaster, userCredentials);
            EnqueueOperation(operation);
            return source.Task;
        }

        public Task<AllEventsSlice> ReadAllEventsForwardAsync(Position position, int maxCount, bool resolveLinkTos, UserCredentials userCredentials = null)
        {
            Ensure.Positive(maxCount, "maxCount");

            var source = new TaskCompletionSource<AllEventsSlice>();
            var operation = new ReadAllEventsForwardOperation(_settings.Log, source, position, maxCount,
                                                              resolveLinkTos, _settings.RequireMaster, userCredentials);
            EnqueueOperation(operation);
            return source.Task;
        }

        public Task<AllEventsSlice> ReadAllEventsBackwardAsync(Position position, int maxCount, bool resolveLinkTos, UserCredentials userCredentials = null)
        {
            Ensure.Positive(maxCount, "maxCount");

            var source = new TaskCompletionSource<AllEventsSlice>();
            var operation = new ReadAllEventsBackwardOperation(_settings.Log, source, position, maxCount,
                                                               resolveLinkTos, _settings.RequireMaster, userCredentials);
            EnqueueOperation(operation);
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

        public Task<EventStoreSubscription> SubscribeToStreamAsync(
                string stream,
                bool resolveLinkTos,
                Action<EventStoreSubscription, ResolvedEvent> eventAppeared,
                Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.NotNull(eventAppeared, "eventAppeared");

            var source = new TaskCompletionSource<EventStoreSubscription>();
            _handler.EnqueueMessage(new StartSubscriptionMessage(source, stream, resolveLinkTos, userCredentials,
                                                                 eventAppeared, subscriptionDropped, 
                                                                 _settings.MaxRetries, _settings.OperationTimeout));
            return source.Task;
        }

        public EventStoreStreamCatchUpSubscription SubscribeToStreamFrom(string stream,
                                                                         int? lastCheckpoint,
                                                                         bool resolveLinkTos,
                                                                         Action<EventStoreCatchUpSubscription, ResolvedEvent> eventAppeared,
                                                                         Action<EventStoreCatchUpSubscription> liveProcessingStarted = null,
                                                                         Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                                                                         UserCredentials userCredentials = null,
                                                                         int readBatchSize = 500)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.NotNull(eventAppeared, "eventAppeared");
            var catchUpSubscription =
                    new EventStoreStreamCatchUpSubscription(this, _settings.Log, stream, lastCheckpoint, 
                                                            resolveLinkTos, userCredentials, eventAppeared, 
                                                            liveProcessingStarted, subscriptionDropped, _settings.VerboseLogging, readBatchSize);
            catchUpSubscription.Start();
            return catchUpSubscription;
        }

        public Task<EventStoreSubscription> SubscribeToAllAsync(
                bool resolveLinkTos, 
                Action<EventStoreSubscription, ResolvedEvent> eventAppeared, 
                Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                UserCredentials userCredentials = null)
        {
            Ensure.NotNull(eventAppeared, "eventAppeared");

            var source = new TaskCompletionSource<EventStoreSubscription>();
            _handler.EnqueueMessage(new StartSubscriptionMessage(source, string.Empty, resolveLinkTos, userCredentials,
                                                                 eventAppeared, subscriptionDropped,
                                                                 _settings.MaxRetries, _settings.OperationTimeout));
            return source.Task;
        }

        public EventStoreAllCatchUpSubscription SubscribeToAllFrom(
                Position? lastCheckpoint,
                bool resolveLinkTos,
                Action<EventStoreCatchUpSubscription, ResolvedEvent> eventAppeared,
                Action<EventStoreCatchUpSubscription> liveProcessingStarted = null,
                Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                UserCredentials userCredentials = null,
                int readBatchSize = 500)
        {
            Ensure.NotNull(eventAppeared, "eventAppeared");
            var catchUpSubscription = 
                    new EventStoreAllCatchUpSubscription(this, _settings.Log, lastCheckpoint, resolveLinkTos,
                                                         userCredentials, eventAppeared, liveProcessingStarted, 
                                                         subscriptionDropped, _settings.VerboseLogging, readBatchSize);
            catchUpSubscription.Start();
            return catchUpSubscription;
        }

        public EventStorePersistentSubscription ConnectToPersistentSubscription(
            string groupName, 
            string stream, 
            Action<EventStorePersistentSubscription, ResolvedEvent> eventAppeared, 
            Action<EventStorePersistentSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
            UserCredentials userCredentials = null, 
            int? bufferSize = null,
            bool autoAck = true)
        {
            Ensure.NotNullOrEmpty(groupName, "groupName");
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.NotNull(eventAppeared, "eventAppeared");

            var subscription = new EventStorePersistentSubscription(
                groupName, stream, eventAppeared, subscriptionDropped, userCredentials, _settings.Log,
                _settings.VerboseLogging, _settings, _handler, bufferSize ?? EventStorePersistentSubscription.DefaultBufferSize, autoAck);

            subscription.Start();

            return subscription;
        }

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


        public Task<PersistentSubscriptionCreateResult> CreatePersistentSubscriptionAsync(string stream, string groupName, bool resolveLinkTos, bool startFromBeginning, UserCredentials userCredentials = null) {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.NotNullOrEmpty(groupName, "groupName");
            var source = new TaskCompletionSource<PersistentSubscriptionCreateResult>();
            EnqueueOperation(new CreatePersistentSubscriptionOperation(_settings.Log, source, stream, groupName, resolveLinkTos, startFromBeginning, userCredentials));
            return source.Task;
        }

        public Task<PersistentSubscriptionCreateResult> CreatePersistentSubscriptionForAllAsync(string groupName, bool resolveLinkTos, bool startFromBeginning, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(groupName, "groupName");
            var source = new TaskCompletionSource<PersistentSubscriptionCreateResult>();
            EnqueueOperation(new CreatePersistentSubscriptionOperation(_settings.Log, source, SystemStreams.AllStream, groupName, resolveLinkTos, startFromBeginning, userCredentials));
            return source.Task;
        }

        public Task<PersistentSubscriptionDeleteResult> DeletePersistentSubscriptionAsync(string stream, string groupName, UserCredentials userCredentials = null) {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.NotNullOrEmpty(groupName, "groupName");
            var source = new TaskCompletionSource<PersistentSubscriptionDeleteResult>();
            EnqueueOperation(new DeletePersistentSubscriptionOperation(_settings.Log, source, stream, groupName, userCredentials));
            return source.Task;            
        }

        public Task<PersistentSubscriptionDeleteResult> DeletePersistentSubscriptionForAllAsync(string groupName, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(groupName, "groupName");
            var source = new TaskCompletionSource<PersistentSubscriptionDeleteResult>();
            EnqueueOperation(new DeletePersistentSubscriptionOperation(_settings.Log, source, SystemStreams.AllStream, groupName, userCredentials));
            return source.Task;
        }


        public Task<WriteResult> SetStreamMetadataAsync(string stream, int expectedMetastreamVersion, StreamMetadata metadata, UserCredentials userCredentials = null)
        {
            return SetStreamMetadataAsync(stream, expectedMetastreamVersion, metadata.AsJsonBytes(), userCredentials);
        }

        public Task<WriteResult> SetStreamMetadataAsync(string stream, int expectedMetastreamVersion, byte[] metadata, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            if (SystemStreams.IsMetastream(stream)) 
                throw new ArgumentException(string.Format("Setting metadata for metastream '{0}' is not supported.", stream), "stream");

            var source = new TaskCompletionSource<WriteResult>();

            var metaevent = new EventData(Guid.NewGuid(), SystemEventTypes.StreamMetadata, true, metadata ?? Empty.ByteArray, null);
            EnqueueOperation(new AppendToStreamOperation(_settings.Log,
                                                         source,
                                                         _settings.RequireMaster,
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
                        return new RawStreamMetadataResult(stream, false, evnt.EventNumber, evnt.Data);
                    case EventReadStatus.NotFound:
                    case EventReadStatus.NoStream:
                        return new RawStreamMetadataResult(stream, false, -1, Empty.ByteArray);
                    case EventReadStatus.StreamDeleted:
                        return new RawStreamMetadataResult(stream, true, int.MaxValue, Empty.ByteArray);
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