using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Internal;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Authentication;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Services;
using EventStore.Core.Util;
using Message = EventStore.Core.Messaging.Message;
using SystemEventTypes = EventStore.ClientAPI.Common.SystemEventTypes;
using SystemStreams = EventStore.ClientAPI.Common.SystemStreams;
#if!NET452
using TaskEx = System.Threading.Tasks.Task;

#endif

namespace EventStore.ClientAPI.Embedded {
	internal class EventStoreEmbeddedNodeConnection : IEventStoreConnection, IEventStoreTransactionConnection {
		private class V8IntegrationAssemblyResolver {
			private readonly string _resourceNamespace;

			public V8IntegrationAssemblyResolver() {
				string environment = Environment.Is64BitProcess
					? "x64"
					: "win32";

				_resourceNamespace = typeof(EventStoreEmbeddedNodeConnection).Namespace + ".libs." + environment;
			}

			public Assembly TryLoadAssemblyFromEmbeddedResource(object sender, ResolveEventArgs e) {
				if (!e.Name.StartsWith("js1"))
					return null;

				byte[] rawAssembly = ReadResource("js1.dll");
				byte[] rawSymbolStore = ReadResource("js1.pdb");

				return Assembly.Load(rawAssembly, rawSymbolStore);
			}


			private byte[] ReadResource(string name) {
				using (Stream stream = typeof(EventStoreEmbeddedNodeConnection)
					.Assembly
					.GetManifestResourceStream(_resourceNamespace + "." + name)) {
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
		private readonly IAuthenticationProvider _authenticationProvider;
		private readonly AuthorizationGateway _authorizationGateway;
		private readonly IBus _subscriptionBus;
		private readonly EmbeddedSubscriber _subscriptions;

		private const int DontReportCheckpointReached = -1;

		static EventStoreEmbeddedNodeConnection() {
			var resolver = new V8IntegrationAssemblyResolver();

			AppDomain.CurrentDomain.AssemblyResolve += resolver.TryLoadAssemblyFromEmbeddedResource;
		}

		public EventStoreEmbeddedNodeConnection(ConnectionSettings settings, string connectionName,
			IPublisher publisher, ISubscriber bus, IAuthenticationProvider authenticationProvider, AuthorizationGateway authorizationGateway) {
			Ensure.NotNull(publisher, nameof(publisher));
			Ensure.NotNull(settings, nameof(settings));
			Ensure.NotNull(authorizationGateway, nameof(authorizationGateway));
			Guid connectionId = Guid.NewGuid();

			_settings = settings;
			_connectionName = connectionName;
			_publisher = new AuthorizingPublisher(publisher, authorizationGateway);
			_authenticationProvider = authenticationProvider;
			_authorizationGateway = authorizationGateway;
			_subscriptionBus = new InMemoryBus("Embedded Client Subscriptions");
			_subscriptions =
				new EmbeddedSubscriber(_subscriptionBus, _authenticationProvider, _settings.Log, connectionId, connectionName);

			_subscriptionBus.Subscribe<ClientMessage.SubscriptionConfirmation>(_subscriptions);
			_subscriptionBus.Subscribe<ClientMessage.SubscriptionDropped>(_subscriptions);
			_subscriptionBus.Subscribe<ClientMessage.StreamEventAppeared>(_subscriptions);
			_subscriptionBus.Subscribe<ClientMessage.CheckpointReached>(_subscriptions);
			_subscriptionBus.Subscribe<ClientMessage.PersistentSubscriptionConfirmation>(_subscriptions);
			_subscriptionBus.Subscribe<ClientMessage.PersistentSubscriptionStreamEventAppeared>(_subscriptions);
			_subscriptionBus.Subscribe(new AdHocHandler<ClientMessage.SubscribeToStream>(_publisher.Publish));
			_subscriptionBus.Subscribe(new AdHocHandler<ClientMessage.FilteredSubscribeToStream>(_publisher.Publish));
			_subscriptionBus.Subscribe(new AdHocHandler<ClientMessage.UnsubscribeFromStream>(_publisher.Publish));
			_subscriptionBus.Subscribe(
				new AdHocHandler<ClientMessage.ConnectToPersistentSubscription>(_publisher.Publish));
			_subscriptionBus.Subscribe(
				new AdHocHandler<ClientMessage.PersistentSubscriptionAckEvents>(_publisher.Publish));
			_subscriptionBus.Subscribe(
				new AdHocHandler<ClientMessage.PersistentSubscriptionNackEvents>(_publisher.Publish));

			bus.Subscribe(new AdHocHandler<SystemMessage.BecomeShutdown>(_ =>
				Disconnected(this, new ClientConnectionEventArgs(this, new IPEndPoint(IPAddress.None, 0)))));
		}

		public string ConnectionName {
			get { return _connectionName; }
		}

		public ConnectionSettings Settings {
			get { return _settings; }
		}

		public Task ConnectAsync() {
			var source = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

			source.SetResult(null);

			Connected(this, new ClientConnectionEventArgs(this, new IPEndPoint(IPAddress.None, 0)));

			return source.Task;
		}

		public void Close() {
			_subscriptionBus.Unsubscribe<ClientMessage.SubscriptionConfirmation>(_subscriptions);
			_subscriptionBus.Unsubscribe<ClientMessage.SubscriptionDropped>(_subscriptions);
			_subscriptionBus.Unsubscribe<ClientMessage.StreamEventAppeared>(_subscriptions);

			Closed(this, new ClientClosedEventArgs(this, "Connection close requested by client."));
		}

		public Task<DeleteResult> DeleteStreamAsync(string stream, long expectedVersion,
			UserCredentials userCredentials = null) {
			return DeleteStreamAsync(stream, expectedVersion, false, GetUserCredentials(_settings, userCredentials));
		}

		public Task<DeleteResult> DeleteStreamAsync(string stream, long expectedVersion, bool hardDelete,
			UserCredentials userCredentials = null) {
			Ensure.NotNullOrEmpty(stream, nameof(stream));

			var source = new TaskCompletionSource<DeleteResult>(TaskCreationOptions.RunContinuationsAsynchronously);

			var envelope =
				new EmbeddedResponseEnvelope(new EmbeddedResponders.DeleteStream(source, stream, expectedVersion));

			Guid corrId = Guid.NewGuid();

			_publisher.PublishWithAuthentication(_authenticationProvider,
				GetUserCredentials(_settings, userCredentials), source.SetException, user =>
					new ClientMessage.DeleteStream(corrId, corrId, envelope, false,
						stream, expectedVersion, hardDelete, user));

			return source.Task;
		}

		public Task<WriteResult> AppendToStreamAsync(string stream, long expectedVersion, params EventData[] events) {
			// ReSharper disable RedundantArgumentDefaultValue
			// ReSharper disable RedundantCast
			return AppendToStreamAsync(stream, expectedVersion, (IEnumerable<EventData>)events, null);
			// ReSharper restore RedundantCast
			// ReSharper restore RedundantArgumentDefaultValue
		}

		public Task<WriteResult> AppendToStreamAsync(string stream, long expectedVersion,
			UserCredentials userCredentials, params EventData[] events) {
			// ReSharper disable RedundantCast
			return AppendToStreamAsync(stream, expectedVersion, (IEnumerable<EventData>)events,
				GetUserCredentials(_settings, userCredentials));
			// ReSharper restore RedundantCast
		}

		public async Task<WriteResult> AppendToStreamAsync(string stream, long expectedVersion, IEnumerable<EventData> events,
			UserCredentials userCredentials = null) {
			// ReSharper disable PossibleMultipleEnumeration
			Ensure.NotNullOrEmpty(stream, nameof(stream));
			Ensure.NotNull(events, nameof(events));

			var source = new TaskCompletionSource<WriteResult>(TaskCreationOptions.RunContinuationsAsynchronously);

			var envelope =
				new EmbeddedResponseEnvelope(new EmbeddedResponders.AppendToStream(source, stream, expectedVersion));

			Guid corrId = Guid.NewGuid();

			_publisher.PublishWithAuthentication(_authenticationProvider,
				GetUserCredentials(_settings, userCredentials), source.SetException, user =>
					new ClientMessage.WriteEvents(corrId, corrId, envelope, false,
						stream, expectedVersion, events.ConvertToEvents(), user));

			return await source.Task.ConfigureAwait(false);
			// ReSharper restore PossibleMultipleEnumeration
		}

		public Task<ConditionalWriteResult> ConditionalAppendToStreamAsync(string stream, long expectedVersion,
			IEnumerable<EventData> events,
			UserCredentials userCredentials = null) {
			// ReSharper disable PossibleMultipleEnumeration
			Ensure.NotNullOrEmpty(stream, nameof(stream));
			Ensure.NotNull(events, nameof(events));

			var source =
				new TaskCompletionSource<ConditionalWriteResult>(TaskCreationOptions.RunContinuationsAsynchronously);

			var envelope =
				new EmbeddedResponseEnvelope(new EmbeddedResponders.ConditionalAppendToStream(source, stream));

			Guid corrId = Guid.NewGuid();

			_publisher.PublishWithAuthentication(_authenticationProvider,
				GetUserCredentials(_settings, userCredentials), source.SetException, user =>
					new ClientMessage.WriteEvents(corrId, corrId, envelope, false,
						stream, expectedVersion, events.ConvertToEvents(), user));

			return source.Task;
			// ReSharper restore PossibleMultipleEnumeration
		}

		public Task<EventStoreTransaction> StartTransactionAsync(string stream, long expectedVersion,
			UserCredentials userCredentials = null) {
			Ensure.NotNullOrEmpty(stream, nameof(stream));

			var source =
				new TaskCompletionSource<EventStoreTransaction>(TaskCreationOptions.RunContinuationsAsynchronously);

			var envelope =
				new EmbeddedResponseEnvelope(
					new EmbeddedResponders.TransactionStart(source, this, stream, expectedVersion));

			Guid corrId = Guid.NewGuid();

			_publisher.PublishWithAuthentication(_authenticationProvider,
				GetUserCredentials(_settings, userCredentials), source.SetException, user =>
					new ClientMessage.TransactionStart(corrId, corrId, envelope,
						false, stream, expectedVersion, user));

			return source.Task;
		}

		public EventStoreTransaction ContinueTransaction(long transactionId, UserCredentials userCredentials = null) {
			Ensure.Nonnegative(transactionId, nameof(transactionId));
			return new EventStoreTransaction(transactionId, GetUserCredentials(_settings, userCredentials), this);
		}

		Task IEventStoreTransactionConnection.TransactionalWriteAsync(EventStoreTransaction transaction,
			IEnumerable<EventData> events, UserCredentials userCredentials) {
			// ReSharper disable PossibleMultipleEnumeration
			Ensure.NotNull(transaction, nameof(transaction));
			Ensure.NotNull(events, nameof(events));

			var source =
				new TaskCompletionSource<EventStoreTransaction>(TaskCreationOptions.RunContinuationsAsynchronously);

			var envelope = new EmbeddedResponseEnvelope(new EmbeddedResponders.TransactionWrite(source, this));

			Guid corrId = Guid.NewGuid();

			_publisher.PublishWithAuthentication(_authenticationProvider,
				GetUserCredentials(_settings, userCredentials), source.SetException, user =>
					new ClientMessage.TransactionWrite(corrId, corrId, envelope,
						false, transaction.TransactionId, events.ConvertToEvents(), user));

			return source.Task;

			// ReSharper restore PossibleMultipleEnumeration
		}

		async Task<WriteResult> IEventStoreTransactionConnection.CommitTransactionAsync(EventStoreTransaction transaction,
			UserCredentials userCredentials) {
			Ensure.NotNull(transaction, nameof(transaction));

			var source = new TaskCompletionSource<WriteResult>(TaskCreationOptions.RunContinuationsAsynchronously);

			var envelope = new EmbeddedResponseEnvelope(new EmbeddedResponders.TransactionCommit(source));

			Guid corrId = Guid.NewGuid();

			_publisher.PublishWithAuthentication(_authenticationProvider,
				GetUserCredentials(_settings, userCredentials), source.SetException, user =>
					new ClientMessage.TransactionCommit(corrId, corrId, envelope,
						false, transaction.TransactionId, user));

			return await source.Task.ConfigureAwait(false);
		}

		public async Task<EventReadResult> ReadEventAsync(string stream, long eventNumber, bool resolveLinkTos,
			UserCredentials userCredentials = null) {
			Ensure.NotNullOrEmpty(stream, nameof(stream));
			if (eventNumber < -1) throw new ArgumentOutOfRangeException("eventNumber");

			var source = new TaskCompletionSource<EventReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);

			var envelope = new EmbeddedResponseEnvelope(new EmbeddedResponders.ReadEvent(source, stream, eventNumber));

			Guid corrId = Guid.NewGuid();

			_publisher.PublishWithAuthentication(_authenticationProvider,
				GetUserCredentials(_settings, userCredentials), source.SetException, user =>
					new ClientMessage.ReadEvent(corrId, corrId, envelope,
						stream, eventNumber, resolveLinkTos, false, user));

			return await source.Task.ConfigureAwait(false);
		}

		public async Task<StreamEventsSlice> ReadStreamEventsForwardAsync(string stream, long start, int count,
			bool resolveLinkTos, UserCredentials userCredentials = null) {
			Ensure.NotNullOrEmpty(stream, nameof(stream));
			Ensure.Nonnegative(start, nameof(start));
			Ensure.Positive(count, nameof(count));
			if (count > ClientApiConstants.MaxReadSize)
				throw new ArgumentException(string.Format(
					"Count should be less than {0}. For larger reads you should page.",
					ClientApiConstants.MaxReadSize));
			var source =
				new TaskCompletionSource<StreamEventsSlice>(TaskCreationOptions.RunContinuationsAsynchronously);

			var envelope =
				new EmbeddedResponseEnvelope(new EmbeddedResponders.ReadStreamForwardEvents(source, stream, start));

			Guid corrId = Guid.NewGuid();

			_publisher.PublishWithAuthentication(_authenticationProvider,
				GetUserCredentials(_settings, userCredentials), source.SetException, user =>
					new ClientMessage.ReadStreamEventsForward(corrId, corrId, envelope,
						stream, start, count, resolveLinkTos, false, null, user));

			return await source.Task.ConfigureAwait(false);
		}

		public async Task<StreamEventsSlice> ReadStreamEventsBackwardAsync(string stream, long start, int count,
			bool resolveLinkTos, UserCredentials userCredentials = null) {
			Ensure.NotNullOrEmpty(stream, nameof(stream));
			Ensure.Positive(count, nameof(count));
			if (count > ClientApiConstants.MaxReadSize)
				throw new ArgumentException(string.Format(
					"Count should be less than {0}. For larger reads you should page.",
					ClientApiConstants.MaxReadSize));
			var source =
				new TaskCompletionSource<StreamEventsSlice>(TaskCreationOptions.RunContinuationsAsynchronously);

			var envelope =
				new EmbeddedResponseEnvelope(new EmbeddedResponders.ReadStreamEventsBackward(source, stream, start));

			Guid corrId = Guid.NewGuid();

			_publisher.PublishWithAuthentication(_authenticationProvider,
				GetUserCredentials(_settings, userCredentials), source.SetException, user =>
					new ClientMessage.ReadStreamEventsBackward(corrId, corrId, envelope,
						stream, start, count, resolveLinkTos, false, null, user));

			return await source.Task.ConfigureAwait(false);
		}

		public async Task<AllEventsSlice> ReadAllEventsForwardAsync(Position position, int maxCount, bool resolveLinkTos,
			UserCredentials userCredentials = null) {
			Ensure.Positive(maxCount, nameof(maxCount));
			if (maxCount > ClientApiConstants.MaxReadSize)
				throw new ArgumentException(string.Format(
					"Count should be less than {0}. For larger reads you should page.",
					ClientApiConstants.MaxReadSize));
			var source = new TaskCompletionSource<AllEventsSlice>(TaskCreationOptions.RunContinuationsAsynchronously);

			var envelope = new EmbeddedResponseEnvelope(new EmbeddedResponders.ReadAllEventsForward(source));

			Guid corrId = Guid.NewGuid();

			_publisher.PublishWithAuthentication(_authenticationProvider,
				GetUserCredentials(_settings, userCredentials), source.SetException, user =>
					new ClientMessage.ReadAllEventsForward(corrId, corrId, envelope,
						position.CommitPosition,
						position.PreparePosition, maxCount, resolveLinkTos, false, null, user));

			return await source.Task.ConfigureAwait(false);
		}

		public Task<AllEventsSlice> FilteredReadAllEventsForwardAsync(Position position, int maxCount,
			bool resolveLinkTos, Filter filter, UserCredentials userCredentials = null) {
			return FilteredReadAllEventsForwardAsync(position, maxCount, resolveLinkTos, filter, maxCount,
				userCredentials);
		}

		public async Task<AllEventsSlice> FilteredReadAllEventsForwardAsync(Position position, int maxCount,
			bool resolveLinkTos, Filter filter, int maxSearchWindow, UserCredentials userCredentials = null) {
			Ensure.Positive(maxCount, nameof(maxCount));
			Ensure.Positive(maxSearchWindow, nameof(maxSearchWindow));
			Ensure.GreaterThanOrEqualTo(maxSearchWindow, maxCount, nameof(maxSearchWindow));
			Ensure.NotNull(filter, nameof(filter));

			if (maxCount > ClientApiConstants.MaxReadSize)
				throw new ArgumentException(string.Format(
					"Count should be less than {0}. For larger reads you should page.",
					ClientApiConstants.MaxReadSize));
			var source = new TaskCompletionSource<AllEventsSlice>(TaskCreationOptions.RunContinuationsAsynchronously);

			var envelope = new EmbeddedResponseEnvelope(new EmbeddedResponders.FilteredReadAllEventsForward(source));

			Guid corrId = Guid.NewGuid();

			var serverFilter = ConvertToServerFilter(filter);

			_publisher.PublishWithAuthentication(_authenticationProvider,
				GetUserCredentials(_settings, userCredentials), source.SetException, user =>
					new ClientMessage.FilteredReadAllEventsForward(corrId, corrId, envelope,
						position.CommitPosition,
						position.PreparePosition, maxCount, resolveLinkTos, false, maxSearchWindow, null,
						EventFilter.Get(serverFilter), user));
			return await source.Task.ConfigureAwait(false);
		}

		public async Task<AllEventsSlice> ReadAllEventsBackwardAsync(Position position, int maxCount, bool resolveLinkTos,
			UserCredentials userCredentials = null) {
			Ensure.Positive(maxCount, nameof(maxCount));
			if (maxCount > ClientApiConstants.MaxReadSize)
				throw new ArgumentException(string.Format(
					"Count should be less than {0}. For larger reads you should page.",
					ClientApiConstants.MaxReadSize));
			var source = new TaskCompletionSource<AllEventsSlice>(TaskCreationOptions.RunContinuationsAsynchronously);
			var envelope = new EmbeddedResponseEnvelope(new EmbeddedResponders.ReadAllEventsBackward(source));
			Guid corrId = Guid.NewGuid();
			_publisher.PublishWithAuthentication(_authenticationProvider,
				GetUserCredentials(_settings, userCredentials), source.SetException, user =>
					new ClientMessage.ReadAllEventsBackward(corrId, corrId, envelope,
						position.CommitPosition,
						position.PreparePosition, maxCount, resolveLinkTos, false, null, user));
			return await source.Task.ConfigureAwait(false);
		}

		public Task<AllEventsSlice> FilteredReadAllEventsBackwardAsync(Position position, int maxCount,
			bool resolveLinkTos, Filter filter, UserCredentials userCredentials = null) {
			return FilteredReadAllEventsBackwardAsync(position, maxCount, resolveLinkTos, filter, maxCount,
				userCredentials);
		}

		public async Task<AllEventsSlice> FilteredReadAllEventsBackwardAsync(Position position, int maxCount,
			bool resolveLinkTos, Filter filter, int maxSearchWindow, UserCredentials userCredentials = null) {
			Ensure.Positive(maxCount, nameof(maxCount));
			Ensure.Positive(maxSearchWindow, nameof(maxSearchWindow));
			Ensure.GreaterThanOrEqualTo(maxSearchWindow, maxCount, nameof(maxSearchWindow));
			Ensure.NotNull(filter, nameof(filter));

			if (maxCount > ClientApiConstants.MaxReadSize)
				throw new ArgumentException(string.Format(
					"Count should be less than {0}. For larger reads you should page.",
					ClientApiConstants.MaxReadSize));
			var source = new TaskCompletionSource<AllEventsSlice>(TaskCreationOptions.RunContinuationsAsynchronously);
			var envelope = new EmbeddedResponseEnvelope(new EmbeddedResponders.FilteredReadAllEventsBackward(source));
			Guid corrId = Guid.NewGuid();

			var serverFilter = ConvertToServerFilter(filter);

			_publisher.PublishWithAuthentication(_authenticationProvider,
				GetUserCredentials(_settings, userCredentials), source.SetException, user =>
					new ClientMessage.FilteredReadAllEventsBackward(corrId, corrId, envelope,
						position.CommitPosition,
						position.PreparePosition, maxCount, resolveLinkTos, false, maxSearchWindow, null,
						EventFilter.Get(serverFilter), user));
			return await source.Task.ConfigureAwait(false);
		}

		public async Task<EventStoreSubscription> SubscribeToStreamAsync(
			string stream,
			bool resolveLinkTos,
			Func<EventStoreSubscription, ResolvedEvent, Task> eventAppeared,
			Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
			UserCredentials userCredentials = null) {
			Ensure.NotNullOrEmpty(stream, nameof(stream));
			Ensure.NotNull(eventAppeared, nameof(eventAppeared));

			var source =
				new TaskCompletionSource<EventStoreSubscription>(TaskCreationOptions.RunContinuationsAsynchronously);

			Guid corrId = Guid.NewGuid();
			_subscriptions.StartSubscription(corrId, source, stream, GetUserCredentials(_settings, userCredentials),
				resolveLinkTos, eventAppeared, subscriptionDropped);
			return await source.Task.ConfigureAwait(false);
		}

		public EventStoreStreamCatchUpSubscription SubscribeToStreamFrom(string stream,
			long? lastCheckpoint,
			bool resolveLinkTos,
			Func<EventStoreCatchUpSubscription, ResolvedEvent, Task> eventAppeared,
			Action<EventStoreCatchUpSubscription> liveProcessingStarted = null,
			Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
			UserCredentials userCredentials = null,
			int readBatchSize = 500,
			string subscriptionName = "") {
			var settings = new CatchUpSubscriptionSettings(Consts.CatchUpDefaultMaxPushQueueSize, readBatchSize,
				_settings.VerboseLogging, resolveLinkTos, subscriptionName);
			return SubscribeToStreamFrom(stream, lastCheckpoint, settings, eventAppeared, liveProcessingStarted,
				subscriptionDropped, userCredentials);
		}

		public EventStoreStreamCatchUpSubscription SubscribeToStreamFrom(
			string stream,
			long? lastCheckpoint,
			CatchUpSubscriptionSettings settings,
			Func<EventStoreCatchUpSubscription, ResolvedEvent, Task> eventAppeared,
			Action<EventStoreCatchUpSubscription> liveProcessingStarted = null,
			Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
			UserCredentials userCredentials = null) {
			Ensure.NotNullOrEmpty(stream, nameof(stream));
			Ensure.NotNull(settings, nameof(settings));
			Ensure.NotNull(eventAppeared, nameof(eventAppeared));

			var catchUpSubscription =
				new EventStoreStreamCatchUpSubscription(this, _settings.Log, stream, lastCheckpoint,
					userCredentials, eventAppeared, liveProcessingStarted,
					subscriptionDropped, settings);

			catchUpSubscription.StartAsync();
			return catchUpSubscription;
		}

		public async Task<EventStoreSubscription> SubscribeToAllAsync(
			bool resolveLinkTos,
			Func<EventStoreSubscription, ResolvedEvent, Task> eventAppeared,
			Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
			UserCredentials userCredentials = null) {
			Ensure.NotNull(eventAppeared, nameof(eventAppeared));

			var source =
				new TaskCompletionSource<EventStoreSubscription>(TaskCreationOptions.RunContinuationsAsynchronously);

			Guid corrId = Guid.NewGuid();
			_subscriptions.StartSubscription(corrId, source, string.Empty,
				GetUserCredentials(_settings, userCredentials), resolveLinkTos, eventAppeared, subscriptionDropped);
			return await source.Task.ConfigureAwait(false);
		}

		public Task<EventStoreSubscription> FilteredSubscribeToAllAsync(bool resolveLinkTos, Filter filter,
			Func<EventStoreSubscription, ResolvedEvent, Task> eventAppeared,
			Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
			UserCredentials userCredentials = null) {
			return FilteredSubscribeToAllAsync(resolveLinkTos, filter, eventAppeared, (s, p) => TaskEx.CompletedTask,
				DontReportCheckpointReached, subscriptionDropped, userCredentials);
		}

		public Task<EventStoreSubscription> FilteredSubscribeToAllAsync(bool resolveLinkTos, Filter filter,
			Func<EventStoreSubscription, ResolvedEvent, Task> eventAppeared,
			Func<EventStoreSubscription, Position, Task> checkpointReached,
			int checkpointInterval,
			Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
			UserCredentials userCredentials = null) {
			Ensure.NotNull(eventAppeared, nameof(eventAppeared));
			Ensure.NotNull(filter, nameof(filter));
			Ensure.NotNull(checkpointReached, nameof(checkpointReached));

			if (checkpointInterval <= 0 && checkpointInterval != DontReportCheckpointReached) {
				throw new ArgumentOutOfRangeException(nameof(checkpointInterval));
			}

			var source =
				new TaskCompletionSource<EventStoreSubscription>(TaskCreationOptions.RunContinuationsAsynchronously);

			var serverFilter = ConvertToServerFilter(filter);

			Guid corrId = Guid.NewGuid();
			_subscriptions.StartFilteredSubscription(corrId, source, string.Empty,
				GetUserCredentials(_settings, userCredentials), resolveLinkTos, serverFilter, eventAppeared,
				checkpointReached, checkpointInterval, subscriptionDropped);
			return source.Task;
		}

		public EventStorePersistentSubscriptionBase ConnectToPersistentSubscription(
			string stream, string groupName,
			Func<EventStorePersistentSubscriptionBase, ResolvedEvent, int?, Task> eventAppeared,
			Action<EventStorePersistentSubscriptionBase, SubscriptionDropReason, Exception> subscriptionDropped = null,
			UserCredentials userCredentials = null, int bufferSize = 10,
			bool autoAck = true) {
			Ensure.NotNullOrEmpty(groupName, nameof(groupName));
			Ensure.NotNullOrEmpty(stream, nameof(stream));
			Ensure.NotNull(eventAppeared, nameof(eventAppeared));

			var subscription = new EmbeddedEventStorePersistentSubscription(groupName, stream, eventAppeared,
				subscriptionDropped,
				GetUserCredentials(_settings, userCredentials), _settings.Log, _settings.VerboseLogging, _settings,
				_subscriptions, bufferSize,
				autoAck);

			subscription.Start().Wait();
			return subscription;
		}

		public async Task<EventStorePersistentSubscriptionBase> ConnectToPersistentSubscriptionAsync(
			string stream, string groupName,
			Func<EventStorePersistentSubscriptionBase, ResolvedEvent, int?, Task> eventAppeared,
			Action<EventStorePersistentSubscriptionBase, SubscriptionDropReason, Exception> subscriptionDropped = null,
			UserCredentials userCredentials = null, int bufferSize = 10, bool autoAck = true) {
			Ensure.NotNull(eventAppeared, nameof(eventAppeared));

			var subscription = new EmbeddedEventStorePersistentSubscription(groupName, stream, eventAppeared,
				subscriptionDropped,
				GetUserCredentials(_settings, userCredentials), _settings.Log, _settings.VerboseLogging, _settings,
				_subscriptions, bufferSize,
				autoAck);
			return await subscription.Start().ConfigureAwait(false);
		}

		public EventStoreAllCatchUpSubscription SubscribeToAllFrom(
			Position? lastCheckpoint,
			bool resolveLinkTos,
			Func<EventStoreCatchUpSubscription, ResolvedEvent, Task> eventAppeared,
			Action<EventStoreCatchUpSubscription> liveProcessingStarted = null,
			Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
			UserCredentials userCredentials = null,
			int readBatchSize = 500,
			string subscriptionName = "") {
			var settings = new CatchUpSubscriptionSettings(Consts.CatchUpDefaultMaxPushQueueSize, readBatchSize,
				_settings.VerboseLogging, resolveLinkTos, subscriptionName);
			return SubscribeToAllFrom(lastCheckpoint, settings, eventAppeared, liveProcessingStarted,
				subscriptionDropped, userCredentials);
		}

		public EventStoreAllCatchUpSubscription SubscribeToAllFrom(
			Position? lastCheckpoint,
			CatchUpSubscriptionSettings settings,
			Func<EventStoreCatchUpSubscription, ResolvedEvent, Task> eventAppeared,
			Action<EventStoreCatchUpSubscription> liveProcessingStarted = null,
			Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
			UserCredentials userCredentials = null) {
			Ensure.NotNull(eventAppeared, nameof(eventAppeared));
			Ensure.NotNull(settings, nameof(settings));

			var catchUpSubscription =
				new EventStoreAllCatchUpSubscription(this, _settings.Log, lastCheckpoint,
					userCredentials, eventAppeared, liveProcessingStarted,
					subscriptionDropped, settings);

			catchUpSubscription.StartAsync();
			return catchUpSubscription;
		}

		public EventStoreAllFilteredCatchUpSubscription FilteredSubscribeToAllFrom(Position? lastCheckpoint,
			Filter filter, CatchUpSubscriptionFilteredSettings settings,
			Func<EventStoreCatchUpSubscription, ResolvedEvent, Task> eventAppeared,
			Action<EventStoreCatchUpSubscription> liveProcessingStarted = null,
			Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
			UserCredentials userCredentials = null) {
			return FilteredSubscribeToAllFrom(lastCheckpoint, filter, settings, eventAppeared,
				(s, p) => TaskEx.CompletedTask, DontReportCheckpointReached, liveProcessingStarted, subscriptionDropped,
				userCredentials);
		}

		public EventStoreAllFilteredCatchUpSubscription FilteredSubscribeToAllFrom(Position? lastCheckpoint,
			Filter filter, CatchUpSubscriptionFilteredSettings settings,
			Func<EventStoreCatchUpSubscription, ResolvedEvent, Task> eventAppeared,
			Func<EventStoreCatchUpSubscription, Position, Task> checkpointReached, int checkpointIntervalMultiplier,
			Action<EventStoreCatchUpSubscription> liveProcessingStarted = null,
			Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
			UserCredentials userCredentials = null) {
			Ensure.NotNull(eventAppeared, nameof(eventAppeared));
			Ensure.NotNull(settings, nameof(settings));

			Ensure.NotNull(filter, nameof(filter));
			Ensure.NotNull(checkpointReached, nameof(checkpointReached));

			if (checkpointIntervalMultiplier <= 0 && checkpointIntervalMultiplier != DontReportCheckpointReached) {
				throw new ArgumentOutOfRangeException(nameof(checkpointIntervalMultiplier));
			}

			var catchUpSubscription =
				new EventStoreAllFilteredCatchUpSubscription(this, _settings.Log, lastCheckpoint, filter,
					userCredentials, eventAppeared, checkpointReached, checkpointIntervalMultiplier,
					liveProcessingStarted,
					subscriptionDropped, settings);

			catchUpSubscription.StartAsync();
			return catchUpSubscription;
		}

		public async Task CreatePersistentSubscriptionAsync(string stream, string groupName,
			PersistentSubscriptionSettings settings,
			UserCredentials credentials) {
			Ensure.NotNullOrEmpty(stream, nameof(stream));
			Ensure.NotNullOrEmpty(groupName, nameof(groupName));
			Ensure.NotNull(settings, nameof(settings));

			var source =
				new TaskCompletionSource<PersistentSubscriptionCreateResult>(TaskCreationOptions
					.RunContinuationsAsynchronously);

			var envelope =
				new EmbeddedResponseEnvelope(
					new EmbeddedResponders.CreatePersistentSubscription(source, stream, groupName));

			var corrId = Guid.NewGuid();
			_publisher.PublishWithAuthentication(_authenticationProvider, GetUserCredentials(_settings, credentials),
				source.SetException, user => new ClientMessage.CreatePersistentSubscription(
					corrId,
					corrId,
					envelope,
					stream,
					groupName,
					settings.ResolveLinkTos,
					settings.StartFrom,
					(int)settings.MessageTimeout.TotalMilliseconds,
					settings.ExtraStatistics,
					settings.MaxRetryCount,
					settings.HistoryBufferSize,
					settings.LiveBufferSize,
					settings.ReadBatchSize,
					(int)settings.CheckPointAfter.TotalMilliseconds,
					settings.MinCheckPointCount,
					settings.MaxCheckPointCount,
					settings.MaxSubscriberCount,
					settings.NamedConsumerStrategy,
					user));
			await source.Task.ConfigureAwait(false);
		}

		public async Task UpdatePersistentSubscriptionAsync(string stream, string groupName,
			PersistentSubscriptionSettings settings,
			UserCredentials credentials) {
			Ensure.NotNullOrEmpty(stream, nameof(stream));
			Ensure.NotNullOrEmpty(groupName, nameof(groupName));

			var source =
				new TaskCompletionSource<PersistentSubscriptionUpdateResult>(TaskCreationOptions
					.RunContinuationsAsynchronously);

			var envelope =
				new EmbeddedResponseEnvelope(
					new EmbeddedResponders.UpdatePersistentSubscription(source, stream, groupName));

			var corrId = Guid.NewGuid();
			_publisher.PublishWithAuthentication(_authenticationProvider, GetUserCredentials(_settings, credentials),
				source.SetException, user => new ClientMessage.UpdatePersistentSubscription(
					corrId,
					corrId,
					envelope,
					stream,
					groupName,
					settings.ResolveLinkTos,
					settings.StartFrom,
					(int)settings.MessageTimeout.TotalMilliseconds,
					settings.ExtraStatistics,
					settings.MaxRetryCount,
					settings.HistoryBufferSize,
					settings.LiveBufferSize,
					settings.ReadBatchSize,
					(int)settings.CheckPointAfter.TotalMilliseconds,
					settings.MinCheckPointCount,
					settings.MaxCheckPointCount,
					settings.MaxSubscriberCount,
					settings.NamedConsumerStrategy,
					user));
			await source.Task.ConfigureAwait(false);
		}


		public async Task DeletePersistentSubscriptionAsync(
			string stream, string groupName, UserCredentials userCredentials = null) {
			Ensure.NotNullOrEmpty(stream, nameof(stream));
			Ensure.NotNullOrEmpty(groupName, nameof(groupName));

			var source =
				new TaskCompletionSource<PersistentSubscriptionDeleteResult>(TaskCreationOptions
					.RunContinuationsAsynchronously);

			var envelope = new EmbeddedResponseEnvelope(
				new EmbeddedResponders.DeletePersistentSubscription(source, stream, groupName));

			var corrId = Guid.NewGuid();
			_publisher.PublishWithAuthentication(_authenticationProvider,
				GetUserCredentials(_settings, userCredentials), source.SetException, user =>
					new ClientMessage.DeletePersistentSubscription(
						corrId,
						corrId,
						envelope,
						stream,
						groupName,
						user));
			await source.Task.ConfigureAwait(false);
		}

		public Task<WriteResult> SetStreamMetadataAsync(string stream, long expectedMetastreamVersion,
			StreamMetadata metadata, UserCredentials userCredentials = null) {
			return SetStreamMetadataAsync(stream, expectedMetastreamVersion, metadata.AsJsonBytes(),
				GetUserCredentials(_settings, userCredentials));
		}

		public async Task<WriteResult> SetStreamMetadataAsync(string stream, long expectedMetastreamVersion, byte[] metadata,
			UserCredentials userCredentials = null) {
			Ensure.NotNullOrEmpty(stream, nameof(stream));
			if (SystemStreams.IsMetastream(stream))
				throw new ArgumentException(
					string.Format("Setting metadata for metastream '{0}' is not supported.", stream), "stream");

			var metaevent = new EventData(Guid.NewGuid(), SystemEventTypes.StreamMetadata, true,
				metadata ?? Empty.ByteArray, null);

			var metastream = SystemStreams.MetastreamOf(stream);
			var source = new TaskCompletionSource<WriteResult>(TaskCreationOptions.RunContinuationsAsynchronously);

			var envelope = new EmbeddedResponseEnvelope(
				new EmbeddedResponders.AppendToStream(source, metastream, expectedMetastreamVersion));

			var corrId = Guid.NewGuid();
			_publisher.PublishWithAuthentication(_authenticationProvider,
				GetUserCredentials(_settings, userCredentials), source.SetException, user =>
					new ClientMessage.WriteEvents(corrId, corrId, envelope, false, metastream,
						expectedMetastreamVersion, metaevent.ConvertToEvent(), user));
			return await source.Task.ConfigureAwait(false);
		}

		public Task<StreamMetadataResult>
			GetStreamMetadataAsync(string stream, UserCredentials userCredentials = null) {
			return GetStreamMetadataAsRawBytesAsync(stream, GetUserCredentials(_settings, userCredentials))
				.ContinueWith(t => {
					if (t.Exception != null)
						throw t.Exception.InnerException;
					var res = t.Result;
					if (res.StreamMetadata == null || res.StreamMetadata.Length == 0)
						return new StreamMetadataResult(res.Stream, res.IsStreamDeleted, res.MetastreamVersion,
							StreamMetadata.Create());
					var metadata = StreamMetadata.FromJsonBytes(res.StreamMetadata);
					return new StreamMetadataResult(res.Stream, res.IsStreamDeleted, res.MetastreamVersion, metadata);
				});
		}

		public Task<RawStreamMetadataResult> GetStreamMetadataAsRawBytesAsync(string stream,
			UserCredentials userCredentials = null) {
			return ReadEventAsync(SystemStreams.MetastreamOf(stream), -1, false,
				GetUserCredentials(_settings, userCredentials)).ContinueWith(t => {
				if (t.Exception != null)
					throw t.Exception.InnerException;

				var res = t.Result;
				switch (res.Status) {
					case EventReadStatus.Success:
						if (res.Event == null)
							throw new Exception("Event is null while operation result is Success.");
						var evnt = res.Event.Value.OriginalEvent;
						if (evnt == null)
							return new RawStreamMetadataResult(stream, false, -1, Empty.ByteArray);
						return new RawStreamMetadataResult(stream, false, evnt.EventNumber, evnt.Data);
					case EventReadStatus.NotFound:
					case EventReadStatus.NoStream:
						return new RawStreamMetadataResult(stream, false, -1, Empty.ByteArray);
					case EventReadStatus.StreamDeleted:
						return new RawStreamMetadataResult(stream, true, long.MaxValue, Empty.ByteArray);
					default:
						throw new ArgumentOutOfRangeException(string.Format("Unexpected ReadEventResult: {0}.",
							res.Status));
				}
			});
		}

		public Task SetSystemSettingsAsync(SystemSettings settings, UserCredentials userCredentials = null) {
			return AppendToStreamAsync(SystemStreams.SettingsStream, ExpectedVersion.Any,
				GetUserCredentials(_settings, userCredentials),
				new EventData(Guid.NewGuid(), SystemEventTypes.Settings, true, settings.ToJsonBytes(), null));
		}

		public event EventHandler<ClientConnectionEventArgs> Connected = delegate { };
		public event EventHandler<ClientConnectionEventArgs> Disconnected = delegate { };

		public event EventHandler<ClientReconnectingEventArgs> Reconnecting {
			add { }
			remove { }
		}

		public event EventHandler<ClientClosedEventArgs> Closed = delegate { };

		public event EventHandler<ClientErrorEventArgs> ErrorOccurred {
			add { }
			remove { }
		}

		public event EventHandler<ClientAuthenticationFailedEventArgs> AuthenticationFailed {
			add { }
			remove { }
		}

		void IDisposable.Dispose() {
			Close();
		}

		public async Task TransactionalWriteAsync(EventStoreTransaction transaction, IEnumerable<EventData> events,
			UserCredentials userCredentials = null) {
			var source =
				new TaskCompletionSource<EventStoreTransaction>(TaskCreationOptions.RunContinuationsAsynchronously);
			var envelope = new EmbeddedResponseEnvelope(
				new EmbeddedResponders.TransactionWrite(source, this));
			Guid corrId = Guid.NewGuid();
			_publisher.PublishWithAuthentication(_authenticationProvider,
				GetUserCredentials(_settings, userCredentials), source.SetException, user =>
					new ClientMessage.TransactionWrite(corrId, corrId, envelope, false,
						transaction.TransactionId, events.ConvertToEvents(), user));
			await source.Task.ConfigureAwait(false);
		}

		public async Task<WriteResult> CommitTransactionAsync(EventStoreTransaction transaction,
			UserCredentials userCredentials = null) {
			var source = new TaskCompletionSource<WriteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
			var envelope = new EmbeddedResponseEnvelope(
				new EmbeddedResponders.TransactionCommit(source));
			Guid corrId = Guid.NewGuid();
			_publisher.PublishWithAuthentication(
				_authenticationProvider, GetUserCredentials(_settings, userCredentials), source.SetException,
				user => new ClientMessage.TransactionCommit(corrId, corrId, envelope, false,
					transaction.TransactionId, user));
			return await source.Task.ConfigureAwait(false);
		}

		private UserCredentials GetUserCredentials(ConnectionSettings settings, UserCredentials givenCredentials) {
			return givenCredentials ?? settings.DefaultUserCredentials;
		}

		private static TcpClientMessageDto.Filter ConvertToServerFilter(Filter filter) {
			var serverContext = filter.Value.Context == Messages.ClientMessage.Filter.FilterContext.StreamId
				? TcpClientMessageDto.Filter.FilterContext.StreamId
				: TcpClientMessageDto.Filter.FilterContext.EventType;

			var serverType = filter.Value.Type == Messages.ClientMessage.Filter.FilterType.Prefix
				? TcpClientMessageDto.Filter.FilterType.Prefix
				: TcpClientMessageDto.Filter.FilterType.Regex;

			var serverFilter = new TcpClientMessageDto.Filter(serverContext, serverType, filter.Value.Data);
			return serverFilter;
		}

		class AuthorizingPublisher : IPublisher {
			private readonly IPublisher _inner;
			private readonly AuthorizationGateway _authorizationGateway;

			public AuthorizingPublisher(IPublisher inner, AuthorizationGateway authorizationGateway) {
				_inner = inner;
				_authorizationGateway = authorizationGateway;
			}

			public void Publish(Message message) {
				_authorizationGateway.Authorize(message, _inner);
			}
		}
	}
}
