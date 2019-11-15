using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EventStore.ClientAPI.ClientOperations;
using EventStore.ClientAPI.Common;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Common.Utils.Threading;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI.Internal {
	/// <summary>
	/// Maintains a full duplex connection to the EventStore
	/// </summary>
	/// <remarks>
	/// An <see cref="EventStoreConnection"/> operates quite differently than say a SqlConnection. Normally
	/// when using an <see cref="EventStoreConnection"/> you want to keep the connection open for a much longer of time than
	/// when you use a SqlConnection. If you prefer the usage pattern of using(new Connection()) .. then you would likely
	/// want to create a FlyWeight on top of the <see cref="EventStoreConnection"/>.
	///
	/// Another difference is that with the <see cref="EventStoreConnection"/> all operations are handled in a full async manner
	/// (even if you call the synchronous behaviors). Many threads can use an <see cref="EventStoreConnection"/> at the same
	/// time or a single thread can make many asynchronous requests. To get the most performance out of the connection
	/// it is generally recommended to use it in this way.
	/// </remarks>
	internal class EventStoreNodeConnection : IEventStoreConnection, IEventStoreTransactionConnection {
		public string ConnectionName { get; }
		private readonly IEndPointDiscoverer _endPointDiscoverer;
		private readonly EventStoreConnectionLogicHandler _handler;

		private const int DontReportCheckpointReached = -1;

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
		internal EventStoreNodeConnection(ConnectionSettings settings, ClusterSettings clusterSettings,
			IEndPointDiscoverer endPointDiscoverer, string connectionName) {
			Ensure.NotNull(settings, "settings");
			Ensure.NotNull(endPointDiscoverer, "endPointDiscoverer");

			ConnectionName = connectionName ?? string.Format("ES-{0}", Guid.NewGuid());
			Settings = settings;
			ClusterSettings = clusterSettings;
			_endPointDiscoverer = endPointDiscoverer;
			_handler = new EventStoreConnectionLogicHandler(this, settings);
		}

		public Task ConnectAsync() {
			var source = TaskCompletionSourceFactory.Create<object>();
			_handler.EnqueueMessage(new StartConnectionMessage(source, _endPointDiscoverer));
			return source.Task;
		}

		void IDisposable.Dispose() {
			Close();
		}

		public void Close() {
			_handler.EnqueueMessage(new CloseConnectionMessage("Connection close requested by client.", null));
		}

		public Task<DeleteResult> DeleteStreamAsync(string stream, long expectedVersion,
			UserCredentials userCredentials = null) {
			return DeleteStreamAsync(stream, expectedVersion, false, userCredentials);
		}

		public async Task<DeleteResult> DeleteStreamAsync(string stream, long expectedVersion, bool hardDelete,
			UserCredentials userCredentials = null) {
			Ensure.NotNullOrEmpty(stream, "stream");

			var source = TaskCompletionSourceFactory.Create<DeleteResult>();
			var operation = new DeleteStreamOperation(Settings.Log, source, Settings.RequireMaster,
				stream, expectedVersion, hardDelete, userCredentials);
			await EnqueueOperation(operation).ConfigureAwait(false);
			return await source.Task.ConfigureAwait(false);
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
			return AppendToStreamAsync(stream, expectedVersion, (IEnumerable<EventData>)events, userCredentials);
// ReSharper restore RedundantCast
		}

		public async Task<WriteResult> AppendToStreamAsync(string stream, long expectedVersion,
			IEnumerable<EventData> events, UserCredentials userCredentials = null) {
// ReSharper disable PossibleMultipleEnumeration
			Ensure.NotNullOrEmpty(stream, "stream");
			Ensure.NotNull(events, "events");

			var source = TaskCompletionSourceFactory.Create<WriteResult>();
			var operation = new AppendToStreamOperation(Settings.Log, source, Settings.RequireMaster,
				stream, expectedVersion, events, userCredentials);
			await EnqueueOperation(operation).ConfigureAwait(false);
			return await source.Task.ConfigureAwait(false);
// ReSharper restore PossibleMultipleEnumeration
		}

		public async Task<ConditionalWriteResult> ConditionalAppendToStreamAsync(string stream, long expectedVersion,
			IEnumerable<EventData> events,
			UserCredentials userCredentials = null) {
			// ReSharper disable PossibleMultipleEnumeration
			Ensure.NotNullOrEmpty(stream, "stream");
			Ensure.NotNull(events, "events");

			var source = TaskCompletionSourceFactory.Create<ConditionalWriteResult>();
			var operation = new ConditionalAppendToStreamOperation(Settings.Log, source, Settings.RequireMaster,
				stream, expectedVersion, events, userCredentials);
			await EnqueueOperation(operation).ConfigureAwait(false);
			return await source.Task.ConfigureAwait(false);
			// ReSharper restore PossibleMultipleEnumeration
		}

		public async Task<EventStoreTransaction> StartTransactionAsync(string stream, long expectedVersion,
			UserCredentials userCredentials = null) {
			Ensure.NotNullOrEmpty(stream, "stream");

			var source = TaskCompletionSourceFactory.Create<EventStoreTransaction>();
			var operation = new StartTransactionOperation(Settings.Log, source, Settings.RequireMaster,
				stream, expectedVersion, this, userCredentials);
			await EnqueueOperation(operation).ConfigureAwait(false);
			return await source.Task.ConfigureAwait(false);
		}

		public EventStoreTransaction ContinueTransaction(long transactionId, UserCredentials userCredentials = null) {
			Ensure.Nonnegative(transactionId, "transactionId");
			return new EventStoreTransaction(transactionId, userCredentials, this);
		}

		async Task IEventStoreTransactionConnection.TransactionalWriteAsync(EventStoreTransaction transaction,
			IEnumerable<EventData> events, UserCredentials userCredentials) {
// ReSharper disable PossibleMultipleEnumeration
			Ensure.NotNull(transaction, "transaction");
			Ensure.NotNull(events, "events");

			var source = TaskCompletionSourceFactory.Create<object>();
			var operation = new TransactionalWriteOperation(Settings.Log, source, Settings.RequireMaster,
				transaction.TransactionId, events, userCredentials);
			await EnqueueOperation(operation).ConfigureAwait(false);
			await source.Task.ConfigureAwait(false);
// ReSharper restore PossibleMultipleEnumeration
		}

		async Task<WriteResult> IEventStoreTransactionConnection.CommitTransactionAsync(
			EventStoreTransaction transaction, UserCredentials userCredentials) {
			Ensure.NotNull(transaction, "transaction");

			var source = TaskCompletionSourceFactory.Create<WriteResult>();
			var operation = new CommitTransactionOperation(Settings.Log, source, Settings.RequireMaster,
				transaction.TransactionId, userCredentials);
			await EnqueueOperation(operation).ConfigureAwait(false);
			return await source.Task.ConfigureAwait(false);
		}


		public async Task<EventReadResult> ReadEventAsync(string stream, long eventNumber, bool resolveLinkTos,
			UserCredentials userCredentials = null) {
			Ensure.NotNullOrEmpty(stream, "stream");
			if (eventNumber < -1) throw new ArgumentOutOfRangeException(nameof(eventNumber));
			var source = TaskCompletionSourceFactory.Create<EventReadResult>();
			var operation = new ReadEventOperation(Settings.Log, source, stream, eventNumber, resolveLinkTos,
				Settings.RequireMaster, userCredentials);
			await EnqueueOperation(operation).ConfigureAwait(false);
			return await source.Task.ConfigureAwait(false);
		}

		public async Task<StreamEventsSlice> ReadStreamEventsForwardAsync(string stream, long start, int count,
			bool resolveLinkTos, UserCredentials userCredentials = null) {
			Ensure.NotNullOrEmpty(stream, "stream");
			Ensure.Nonnegative(start, "start");
			Ensure.Positive(count, "count");
			if (count > ClientApiConstants.MaxReadSize)
				throw new ArgumentException(string.Format(
					"Count should be less than {0}. For larger reads you should page.",
					ClientApiConstants.MaxReadSize));
			var source = TaskCompletionSourceFactory.Create<StreamEventsSlice>();
			var operation = new ReadStreamEventsForwardOperation(Settings.Log, source, stream, start, count,
				resolveLinkTos, Settings.RequireMaster, userCredentials);
			await EnqueueOperation(operation).ConfigureAwait(false);
			return await source.Task.ConfigureAwait(false);
		}

		public async Task<StreamEventsSlice> ReadStreamEventsBackwardAsync(string stream, long start, int count,
			bool resolveLinkTos, UserCredentials userCredentials = null) {
			Ensure.NotNullOrEmpty(stream, "stream");
			Ensure.Positive(count, "count");
			Ensure.GreaterThanOrEqualTo(start, StreamPosition.End, nameof(start));
			if (count > ClientApiConstants.MaxReadSize)
				throw new ArgumentException(string.Format(
					"Count should be less than {0}. For larger reads you should page.",
					ClientApiConstants.MaxReadSize));
			var source = TaskCompletionSourceFactory.Create<StreamEventsSlice>();
			var operation = new ReadStreamEventsBackwardOperation(Settings.Log, source, stream, start, count,
				resolveLinkTos, Settings.RequireMaster, userCredentials);
			await EnqueueOperation(operation).ConfigureAwait(false);
			return await source.Task.ConfigureAwait(false);
		}

		public async Task<AllEventsSlice> ReadAllEventsForwardAsync(Position position, int maxCount,
			bool resolveLinkTos, UserCredentials userCredentials = null) {
			Ensure.Positive(maxCount, "maxCount");
			if (maxCount > ClientApiConstants.MaxReadSize)
				throw new ArgumentException(string.Format(
					"Count should be less than {0}. For larger reads you should page.",
					ClientApiConstants.MaxReadSize));
			var source = TaskCompletionSourceFactory.Create<AllEventsSlice>();
			var operation = new ReadAllEventsForwardOperation(Settings.Log, source, position, maxCount,
				resolveLinkTos, Settings.RequireMaster, userCredentials);
			await EnqueueOperation(operation).ConfigureAwait(false);
			return await source.Task.ConfigureAwait(false);
		}

		
		public Task<AllEventsSlice> FilteredReadAllEventsForwardAsync(Position position, int maxCount,
			bool resolveLinkTos, Filter filter, UserCredentials userCredentials = null) {
			return FilteredReadAllEventsForwardAsync(position, maxCount, resolveLinkTos, filter, maxCount,
				userCredentials);
		}

		public async Task<AllEventsSlice> FilteredReadAllEventsForwardAsync(Position position, int maxCount,
			bool resolveLinkTos, Filter filter, int maxSearchWindow, UserCredentials userCredentials = null) {
			Ensure.Positive(maxCount, "maxCount");
			Ensure.Positive(maxSearchWindow, nameof(maxSearchWindow));
			Ensure.GreaterThanOrEqualTo(maxSearchWindow, maxCount, nameof(maxSearchWindow));
			Ensure.NotNull(filter, nameof(filter));

			if (maxCount > ClientApiConstants.MaxReadSize)
				throw new ArgumentException(string.Format(
					"Count should be less than {0}. For larger reads you should page.",
					ClientApiConstants.MaxReadSize));

			var source = TaskCompletionSourceFactory.Create<AllEventsSlice>();
			var operation = new FilteredReadAllEventsForwardOperation(Settings.Log, source, position, maxCount,
				resolveLinkTos, Settings.RequireMaster, maxSearchWindow, filter.Value, userCredentials);

			await EnqueueOperation(operation).ConfigureAwait(false);
			return await source.Task.ConfigureAwait(false);
		}

		public async Task<AllEventsSlice> ReadAllEventsBackwardAsync(Position position, int maxCount,
			bool resolveLinkTos, UserCredentials userCredentials = null) {
			Ensure.Positive(maxCount, "maxCount");
			if (maxCount > ClientApiConstants.MaxReadSize)
				throw new ArgumentException(string.Format(
					"Count should be less than {0}. For larger reads you should page.",
					ClientApiConstants.MaxReadSize));
			var source = TaskCompletionSourceFactory.Create<AllEventsSlice>();
			var operation = new ReadAllEventsBackwardOperation(Settings.Log, source, position, maxCount,
				resolveLinkTos, Settings.RequireMaster, userCredentials);
			await EnqueueOperation(operation).ConfigureAwait(false);
			return await source.Task.ConfigureAwait(false);
		}

		public Task<AllEventsSlice> FilteredReadAllEventsBackwardAsync(Position position, int maxCount,
			bool resolveLinkTos, Filter filter, UserCredentials userCredentials = null) {
			return FilteredReadAllEventsBackwardAsync(position, maxCount, resolveLinkTos, filter, maxCount,
				userCredentials);
		}

		public async Task<AllEventsSlice> FilteredReadAllEventsBackwardAsync(Position position, int maxCount,
			bool resolveLinkTos, Filter filter, int maxSearchWindow, UserCredentials userCredentials = null) {
			Ensure.Positive(maxCount, "maxCount");
			Ensure.Positive(maxSearchWindow, nameof(maxSearchWindow));
			Ensure.GreaterThanOrEqualTo(maxSearchWindow, maxCount, nameof(maxSearchWindow));
			Ensure.NotNull(filter, nameof(filter));

			if (maxCount > ClientApiConstants.MaxReadSize)
				throw new ArgumentException(string.Format(
					"Count should be less than {0}. For larger reads you should page.",
					ClientApiConstants.MaxReadSize));

			var source = TaskCompletionSourceFactory.Create<AllEventsSlice>();
			var operation = new FilteredReadAllEventsBackwardOperation(Settings.Log, source, position, maxCount,
				resolveLinkTos, Settings.RequireMaster, maxSearchWindow, filter.Value, userCredentials);

			await EnqueueOperation(operation).ConfigureAwait(false);
			return await source.Task.ConfigureAwait(false);
		}

		private async Task EnqueueOperation(IClientOperation operation) {
			while (_handler.TotalOperationCount >= Settings.MaxQueueSize) {
				await Task.Delay(1).ConfigureAwait(false);
			}

			_handler.EnqueueMessage(
				new StartOperationMessage(operation, Settings.MaxRetries, Settings.OperationTimeout));
		}

		public Task<EventStoreSubscription> SubscribeToStreamAsync(
			string stream,
			bool resolveLinkTos,
			Func<EventStoreSubscription, ResolvedEvent, Task> eventAppeared,
			Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
			UserCredentials userCredentials = null) {
			Ensure.NotNullOrEmpty(stream, "stream");
			Ensure.NotNull(eventAppeared, "eventAppeared");

			var source = TaskCompletionSourceFactory.Create<EventStoreSubscription>();
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
			string subscriptionName = "") {
			var settings = new CatchUpSubscriptionSettings(Consts.CatchUpDefaultMaxPushQueueSize, readBatchSize,
				Settings.VerboseLogging,
				resolveLinkTos,
				subscriptionName);
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
			UserCredentials userCredentials = null) {
			Ensure.NotNull(eventAppeared, "eventAppeared");

			var source = TaskCompletionSourceFactory.Create<EventStoreSubscription>();
			_handler.EnqueueMessage(new StartSubscriptionMessage(source, string.Empty, resolveLinkTos, userCredentials,
				eventAppeared, subscriptionDropped,
				Settings.MaxRetries, Settings.OperationTimeout));
			return source.Task;
		}

		public Task<EventStoreSubscription> FilteredSubscribeToAllAsync(bool resolveLinkTos, Filter filter,
			Func<EventStoreSubscription, ResolvedEvent, Task> eventAppeared,
			Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
			UserCredentials userCredentials = null) {
			return FilteredSubscribeToAllAsync(resolveLinkTos, filter, eventAppeared, (s, p) => Task.CompletedTask,
				DontReportCheckpointReached, subscriptionDropped, userCredentials);
		}

		public Task<EventStoreSubscription> FilteredSubscribeToAllAsync(bool resolveLinkTos, Filter filter,
			Func<EventStoreSubscription, ResolvedEvent, Task> eventAppeared,
			Func<EventStoreSubscription, Position, Task> checkpointReached, int checkpointInterval,
			Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
			UserCredentials userCredentials = null) {
			Ensure.NotNull(eventAppeared, nameof(eventAppeared));
			Ensure.NotNull(filter, nameof(filter));
			Ensure.NotNull(checkpointReached, nameof(checkpointReached));

			if (checkpointInterval <= 0 && checkpointInterval != DontReportCheckpointReached) {
				throw new ArgumentOutOfRangeException(nameof(checkpointInterval));
			}

			var source = TaskCompletionSourceFactory.Create<EventStoreSubscription>();
			_handler.EnqueueMessage(new StartFilteredSubscriptionMessage(source, string.Empty, resolveLinkTos,
				checkpointInterval, filter, userCredentials, eventAppeared, checkpointReached,
				subscriptionDropped, Settings.MaxRetries, Settings.OperationTimeout));
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
			string subscriptionName = "") {
			var settings = new CatchUpSubscriptionSettings(Consts.CatchUpDefaultMaxPushQueueSize, readBatchSize,
				Settings.VerboseLogging, resolveLinkTos, subscriptionName);
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
			bool autoAck = true) {
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
			bool autoAck = true) {
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


		public async Task CreatePersistentSubscriptionAsync(string stream, string groupName,
			PersistentSubscriptionSettings settings, UserCredentials credentials = null) {
			Ensure.NotNullOrEmpty(stream, "stream");
			Ensure.NotNullOrEmpty(groupName, "groupName");
			Ensure.NotNull(settings, "settings");
			var source = TaskCompletionSourceFactory.Create<PersistentSubscriptionCreateResult>();
			var operation =
				new CreatePersistentSubscriptionOperation(Settings.Log, source, stream, groupName, settings,
					credentials);
			await EnqueueOperation(operation).ConfigureAwait(false);
			await source.Task.ConfigureAwait(false);
		}

		public async Task UpdatePersistentSubscriptionAsync(string stream, string groupName,
			PersistentSubscriptionSettings settings, UserCredentials credentials = null) {
			Ensure.NotNullOrEmpty(stream, "stream");
			Ensure.NotNullOrEmpty(groupName, "groupName");
			Ensure.NotNull(settings, "settings");
			var source = TaskCompletionSourceFactory.Create<PersistentSubscriptionUpdateResult>();
			var operation =
				new UpdatePersistentSubscriptionOperation(Settings.Log, source, stream, groupName, settings,
					credentials);
			await EnqueueOperation(operation).ConfigureAwait(false);
			await source.Task.ConfigureAwait(false);
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
		public async Task DeletePersistentSubscriptionAsync(string stream, string groupName,
			UserCredentials userCredentials = null) {
			Ensure.NotNullOrEmpty(stream, "stream");
			Ensure.NotNullOrEmpty(groupName, "groupName");
			var source = TaskCompletionSourceFactory.Create<PersistentSubscriptionDeleteResult>();
			var operation =
				new DeletePersistentSubscriptionOperation(Settings.Log, source, stream, groupName, userCredentials);
			await EnqueueOperation(operation).ConfigureAwait(false);
			await source.Task.ConfigureAwait(false);
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

		public Task<WriteResult> SetStreamMetadataAsync(string stream, long expectedMetastreamVersion,
			StreamMetadata metadata, UserCredentials userCredentials = null) {
			return SetStreamMetadataAsync(stream, expectedMetastreamVersion, metadata.AsJsonBytes(), userCredentials);
		}

		public async Task<WriteResult> SetStreamMetadataAsync(string stream, long expectedMetastreamVersion,
			byte[] metadata, UserCredentials userCredentials = null) {
			Ensure.NotNullOrEmpty(stream, "stream");
			if (SystemStreams.IsMetastream(stream))
				throw new ArgumentException(
					string.Format("Setting metadata for metastream '{0}' is not supported.", stream), nameof(stream));

			var source = TaskCompletionSourceFactory.Create<WriteResult>();

			var metaevent = new EventData(Guid.NewGuid(), SystemEventTypes.StreamMetadata, true,
				metadata ?? Empty.ByteArray, null);
			var operation = new AppendToStreamOperation(Settings.Log,
				source,
				Settings.RequireMaster,
				SystemStreams.MetastreamOf(stream),
				expectedMetastreamVersion,
				new[] {metaevent},
				userCredentials);
			await EnqueueOperation(operation).ConfigureAwait(false);

			return await source.Task.ConfigureAwait(false);
		}

		public Task<StreamMetadataResult>
			GetStreamMetadataAsync(string stream, UserCredentials userCredentials = null) {
			return GetStreamMetadataAsRawBytesAsync(stream, userCredentials).ContinueWith(t => {
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
			return ReadEventAsync(SystemStreams.MetastreamOf(stream), -1, false, userCredentials).ContinueWith(t => {
				if (t.Exception != null)
					throw t.Exception.InnerException;

				var res = t.Result;
				switch (res.Status) {
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
						throw new ArgumentOutOfRangeException(string.Format("Unexpected ReadEventResult: {0}.",
							res.Status));
				}
			});
		}

		public Task SetSystemSettingsAsync(SystemSettings settings, UserCredentials userCredentials = null) {
			return AppendToStreamAsync(SystemStreams.SettingsStream, ExpectedVersion.Any, userCredentials,
				new EventData(Guid.NewGuid(), SystemEventTypes.Settings, true, settings.ToJsonBytes(), null));
		}

		public event EventHandler<ClientConnectionEventArgs> Connected {
			add { _handler.Connected += value; }
			remove { _handler.Connected -= value; }
		}

		public event EventHandler<ClientConnectionEventArgs> Disconnected {
			add { _handler.Disconnected += value; }
			remove { _handler.Disconnected -= value; }
		}

		public event EventHandler<ClientReconnectingEventArgs> Reconnecting {
			add { _handler.Reconnecting += value; }
			remove { _handler.Reconnecting -= value; }
		}

		public event EventHandler<ClientClosedEventArgs> Closed {
			add { _handler.Closed += value; }
			remove { _handler.Closed -= value; }
		}

		public event EventHandler<ClientErrorEventArgs> ErrorOccurred {
			add { _handler.ErrorOccurred += value; }
			remove { _handler.ErrorOccurred -= value; }
		}

		public event EventHandler<ClientAuthenticationFailedEventArgs> AuthenticationFailed {
			add { _handler.AuthenticationFailed += value; }
			remove { _handler.AuthenticationFailed -= value; }
		}
	}
}
