using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using EventStore.ClientAPI.Common;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Core;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Services.UserManagement;

namespace EventStore.ClientAPI.Embedded
{
    internal class EventStoreEmbeddedNodeConnection : IEventStoreConnection, IEventStoreTransactionConnection
    {
        private class V8IntegrationAssemblyResolver
        {
            private readonly string _resourceNamespace;

            public V8IntegrationAssemblyResolver()
            {
                string environment = Environment.Is64BitProcess
                    ? "x64"
                    : "win32";

                _resourceNamespace = typeof (EventStoreEmbeddedNodeConnection).Namespace + ".libs." + environment;
            }

            public Assembly TryLoadAssemblyFromEmbeddedResource(object sender, ResolveEventArgs e)
            {
                if (!e.Name.StartsWith("js1")) return null;

                byte[] rawAssembly = ReadResource("js1.dll");
                byte[] rawSymbolStore = ReadResource("js1.pdb");

                return Assembly.Load(rawAssembly, rawSymbolStore);
            }


            private byte[] ReadResource(string name)
            {
                using (Stream stream = typeof (EventStoreEmbeddedNodeConnection)
                    .Assembly
                    .GetManifestResourceStream(_resourceNamespace + "." + name))
                {
                    if (stream == null)
                        throw new ArgumentNullException("name");

                    var resource = new byte[stream.Length];

                    stream.Read(resource, 0, resource.Length);

                    return resource;
                }
            }
        }

        private readonly ConnectionSettings _settings;
        private readonly string _connectionName;
        private readonly IPublisher _publisher;
        private readonly IBus _subscriptionBus;
        private readonly EmbeddedSubscriber _subscriptions;

        static EventStoreEmbeddedNodeConnection()
        {
            var resolver = new V8IntegrationAssemblyResolver();

            AppDomain.CurrentDomain.AssemblyResolve += resolver.TryLoadAssemblyFromEmbeddedResource;
        }

        public EventStoreEmbeddedNodeConnection(ConnectionSettings settings, string connectionName, IPublisher publisher, ISubscriber bus)
        {
            Ensure.NotNull(publisher, "publisher");
            Ensure.NotNull(settings, "settings");

            _settings = settings;
            _connectionName = connectionName;
            _publisher = publisher;
            _subscriptionBus = new InMemoryBus("Embedded Client Subscriptions");
            Guid connectionId = Guid.NewGuid();

            _subscriptions = new EmbeddedSubscriber(_settings.Log, connectionId);
            
            _subscriptionBus.Subscribe<ClientMessage.SubscriptionConfirmation>(_subscriptions);
            _subscriptionBus.Subscribe<ClientMessage.SubscriptionDropped>(_subscriptions);
            _subscriptionBus.Subscribe<ClientMessage.StreamEventAppeared>(_subscriptions);
            _subscriptionBus.Subscribe(new AdHocHandler<ClientMessage.SubscribeToStream>(_publisher.Publish));
            _subscriptionBus.Subscribe(new AdHocHandler<ClientMessage.UnsubscribeFromStream>(_publisher.Publish));

            bus.Subscribe(new AdHocHandler<SystemMessage.BecomeShutdown>(_ => Disconnected(this, new ClientConnectionEventArgs(this, new IPEndPoint(IPAddress.None, 0)))));
        }

        public string ConnectionName { get { return _connectionName; } }

        public Task ConnectAsync()
        {
            var source = new TaskCompletionSource<object>();
            
            source.SetResult(null);

            Connected(this, new ClientConnectionEventArgs(this, new IPEndPoint(IPAddress.None, 0)));

            return source.Task;
        }

        public void Close()
        {
            _subscriptionBus.Unsubscribe<ClientMessage.SubscriptionConfirmation>(_subscriptions);
            _subscriptionBus.Unsubscribe<ClientMessage.SubscriptionDropped>(_subscriptions);
            _subscriptionBus.Unsubscribe<ClientMessage.StreamEventAppeared>(_subscriptions);

            Closed(this, new ClientClosedEventArgs(this, "Connection close requested by client."));
        }

        public Task<DeleteResult> DeleteStreamAsync(string stream, int expectedVersion, UserCredentials userCredentials = null)
        {
            return DeleteStreamAsync(stream, expectedVersion, false, userCredentials);
        }

        public Task<DeleteResult> DeleteStreamAsync(string stream, int expectedVersion, bool hardDelete, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(stream, "stream");

            var source = new TaskCompletionSource<DeleteResult>();

            var envelope = new EmbeddedResponseEnvelope(new EmbeddedResponders.DeleteStream(source, stream, expectedVersion));

            Guid corrId = Guid.NewGuid();

            _publisher.Publish(new ClientMessage.DeleteStream(corrId, corrId, envelope, false,
                stream, expectedVersion, hardDelete, SystemAccount.Principal));

            return source.Task;
        }

        public Task<WriteResult> AppendToStreamAsync(string stream, int expectedVersion, params EventData[] events)
        {
            // ReSharper disable RedundantArgumentDefaultValue
            // ReSharper disable RedundantCast
            return AppendToStreamAsync(stream, expectedVersion, (IEnumerable<EventData>)events, null);
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

            var envelope = new EmbeddedResponseEnvelope(new EmbeddedResponders.AppendToStream(source, stream, expectedVersion));

            Guid corrId = Guid.NewGuid();

            _publisher.Publish(new ClientMessage.WriteEvents(corrId, corrId, envelope, false,
                stream, expectedVersion, events.ConvertToEvents(), SystemAccount.Principal));

            return source.Task;
            // ReSharper restore PossibleMultipleEnumeration
        }

        public Task<EventStoreTransaction> StartTransactionAsync(string stream, int expectedVersion, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(stream, "stream");

            var source = new TaskCompletionSource<EventStoreTransaction>();

            var envelope = new EmbeddedResponseEnvelope(new EmbeddedResponders.TransactionStart(source, this, stream, expectedVersion));

            Guid corrId = Guid.NewGuid();

            _publisher.Publish(new ClientMessage.TransactionStart(corrId, corrId, envelope,
                false, stream, expectedVersion, SystemAccount.Principal));

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

            var source = new TaskCompletionSource<EventStoreTransaction>();

            var envelope = new EmbeddedResponseEnvelope(new EmbeddedResponders.TransactionWrite(source, this));

            Guid corrId = Guid.NewGuid();

            _publisher.Publish(new ClientMessage.TransactionWrite(corrId, corrId, envelope,
                false, transaction.TransactionId, events.ConvertToEvents(), SystemAccount.Principal));

            return source.Task;

            // ReSharper restore PossibleMultipleEnumeration
        }

        Task<WriteResult> IEventStoreTransactionConnection.CommitTransactionAsync(EventStoreTransaction transaction, UserCredentials userCredentials)
        {
            Ensure.NotNull(transaction, "transaction");

            var source = new TaskCompletionSource<WriteResult>();
            
            var envelope = new EmbeddedResponseEnvelope(new EmbeddedResponders.TransactionCommit(source));

            Guid corrId = Guid.NewGuid();

            _publisher.Publish(new ClientMessage.TransactionCommit(corrId, corrId, envelope,
                false, transaction.TransactionId, SystemAccount.Principal));

            return source.Task;
        }

        public Task<EventReadResult> ReadEventAsync(string stream, int eventNumber, bool resolveLinkTos, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            if (eventNumber < -1) throw new ArgumentOutOfRangeException("eventNumber");

            var source = new TaskCompletionSource<EventReadResult>();
            
            var envelope = new EmbeddedResponseEnvelope(new EmbeddedResponders.ReadEvent(source, stream, eventNumber));

            Guid corrId = Guid.NewGuid();

            var message = new ClientMessage.ReadEvent(corrId, corrId, envelope,
                stream, eventNumber, resolveLinkTos, false, SystemAccount.Principal);

            _publisher.Publish(message);

            return source.Task;
        }

        public Task<StreamEventsSlice> ReadStreamEventsForwardAsync(string stream, int start, int count, bool resolveLinkTos, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.Nonnegative(start, "start");
            Ensure.Positive(count, "count");

            var source = new TaskCompletionSource<StreamEventsSlice>();

            var envelope = new EmbeddedResponseEnvelope(new EmbeddedResponders.ReadStreamForwardEvents(source, stream, start));

            Guid corrId = Guid.NewGuid();

            var message = new ClientMessage.ReadStreamEventsForward(corrId, corrId, envelope,
                stream, start, count, resolveLinkTos, false, null, SystemAccount.Principal);

            _publisher.Publish(message);

            return source.Task;
        }

        public Task<StreamEventsSlice> ReadStreamEventsBackwardAsync(string stream, int start, int count, bool resolveLinkTos, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Ensure.Positive(count, "count");

            var source = new TaskCompletionSource<StreamEventsSlice>();

            var envelope = new EmbeddedResponseEnvelope(new EmbeddedResponders.ReadStreamEventsBackward(source, stream, start));

            Guid corrId = Guid.NewGuid();

            var message = new ClientMessage.ReadStreamEventsBackward(corrId, corrId, envelope,
                stream, start, count, resolveLinkTos, false, null, SystemAccount.Principal);

            _publisher.Publish(message);

            return source.Task;
        }

        public Task<AllEventsSlice> ReadAllEventsForwardAsync(Position position, int maxCount, bool resolveLinkTos, UserCredentials userCredentials = null)
        {
            Ensure.Positive(maxCount, "maxCount");

            var source = new TaskCompletionSource<AllEventsSlice>();

            var envelope = new EmbeddedResponseEnvelope(new EmbeddedResponders.ReadAllEventsForward(source));

            Guid corrId = Guid.NewGuid();

            var message = new ClientMessage.ReadAllEventsForward(corrId, corrId, envelope,
                position.CommitPosition,
                position.PreparePosition, maxCount, resolveLinkTos, false, null, SystemAccount.Principal);

            _publisher.Publish(message);

            return source.Task;
        }


        public Task<AllEventsSlice> ReadAllEventsBackwardAsync(Position position, int maxCount, bool resolveLinkTos, UserCredentials userCredentials = null)
        {
            Ensure.Positive(maxCount, "maxCount");

            var source = new TaskCompletionSource<AllEventsSlice>();

            var envelope = new EmbeddedResponseEnvelope(new EmbeddedResponders.ReadAllEventsBackward(source));

            Guid corrId = Guid.NewGuid();

            var message = new ClientMessage.ReadAllEventsBackward(corrId, corrId, envelope,
                position.CommitPosition,
                position.PreparePosition, maxCount, resolveLinkTos, false, null, SystemAccount.Principal);

            _publisher.Publish(message);

            return source.Task;
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

            Guid corrId = Guid.NewGuid();

            _subscriptions.Start(_subscriptionBus, corrId, source, stream, resolveLinkTos, eventAppeared, subscriptionDropped);
            
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
        
            Guid corrId = Guid.NewGuid();

            _subscriptions.Start(_subscriptionBus, corrId, source, string.Empty, resolveLinkTos,  eventAppeared, subscriptionDropped);

            return source.Task;
        }

        public EventStorePersistentSubscription ConnectToPersistentSubscription(string groupName, string stream, Action<EventStorePersistentSubscription, ResolvedEvent> eventAppeared,
            Action<EventStorePersistentSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null, UserCredentials userCredentials = null, int bufferSize = 10,
            bool autoAck = true)
        {
            throw new NotImplementedException();
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

        public Task CreatePersistentSubscriptionAsync(string stream, string groupName, PersistentSubscriptionSettings settings,
            UserCredentials credentials)
        {
            throw new NotImplementedException();
        }

        public Task UpdatePersistentSubscriptionAsync(string stream, string groupName, PersistentSubscriptionSettings settings,
            UserCredentials credentials)
        {
            throw new NotImplementedException();
        }


        public Task DeletePersistentSubscriptionAsync(string stream, string groupName, UserCredentials userCredentials = null)
        {
            throw new NotImplementedException();
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

            var metaevent = new EventData(Guid.NewGuid(), SystemEventTypes.StreamMetadata, true, metadata ?? Empty.ByteArray, null);
            var metastream = SystemStreams.MetastreamOf(stream);

            var source = new TaskCompletionSource<WriteResult>();

            var envelope = new EmbeddedResponseEnvelope(new EmbeddedResponders.AppendToStream(source, metastream, expectedMetastreamVersion));

            var corrId = Guid.NewGuid();

            _publisher.Publish(new ClientMessage.WriteEvents(corrId, corrId, envelope, false, metastream, expectedMetastreamVersion, metaevent.ConvertToEvent(), SystemAccount.Principal));

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

        public event EventHandler<ClientConnectionEventArgs> Connected = delegate { };
        public event EventHandler<ClientConnectionEventArgs> Disconnected = delegate { };
        public event EventHandler<ClientReconnectingEventArgs> Reconnecting
        {
            add { }
            remove { }
        }
        public event EventHandler<ClientClosedEventArgs> Closed = delegate { };
        public event EventHandler<ClientErrorEventArgs> ErrorOccurred
        {
            add { }
            remove { }
        }
        public event EventHandler<ClientAuthenticationFailedEventArgs> AuthenticationFailed
        {
            add { }
            remove { }
        }

        void IDisposable.Dispose()
        {
            Close();
        }

        public Task TransactionalWriteAsync(EventStoreTransaction transaction, IEnumerable<EventData> events,
            UserCredentials userCredentials = null)
        {
            var source = new TaskCompletionSource<EventStoreTransaction>();

            var envelope = new EmbeddedResponseEnvelope(new EmbeddedResponders.TransactionWrite(source, this));

            Guid corrId = Guid.NewGuid();

            var message = new ClientMessage.TransactionWrite(corrId, corrId, envelope,false, 
                transaction.TransactionId, events.ConvertToEvents(), SystemAccount.Principal);

            _publisher.Publish(message);

            return source.Task;
            
        }

        public Task<WriteResult> CommitTransactionAsync(EventStoreTransaction transaction, UserCredentials userCredentials = null)
        {
            var source = new TaskCompletionSource<WriteResult>();

            var envelope = new EmbeddedResponseEnvelope(new EmbeddedResponders.TransactionCommit(source));

            Guid corrId = Guid.NewGuid();

            var message = new ClientMessage.TransactionCommit(corrId, corrId, envelope, false,
                transaction.TransactionId, SystemAccount.Principal);

            _publisher.Publish(message);

            return source.Task;
            
        }
    }
}