using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;

namespace EventStore.Core.Tests.ClientAPI;

public class GrpcEventStoreConnection : IEventStoreConnection {
	public void Dispose() {
		throw new NotImplementedException();
	}

	public Task ConnectAsync() {
		throw new NotImplementedException();
	}

	public void Close() {
		throw new NotImplementedException();
	}

	public Task<DeleteResult> DeleteStreamAsync(string stream, long expectedVersion, UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<DeleteResult> DeleteStreamAsync(string stream, long expectedVersion, bool hardDelete, UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<WriteResult> AppendToStreamAsync(string stream, long expectedVersion, params EventData[] events) {
		throw new NotImplementedException();
	}

	public Task<WriteResult> AppendToStreamAsync(string stream, long expectedVersion, UserCredentials userCredentials,
		params EventData[] events) {
		throw new NotImplementedException();
	}

	public Task<WriteResult> AppendToStreamAsync(string stream, long expectedVersion, IEnumerable<EventData> events,
		UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<ConditionalWriteResult> ConditionalAppendToStreamAsync(string stream, long expectedVersion, IEnumerable<EventData> events,
		UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<EventStoreTransaction> StartTransactionAsync(string stream, long expectedVersion, UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public EventStoreTransaction ContinueTransaction(long transactionId, UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<EventReadResult> ReadEventAsync(string stream, long eventNumber, bool resolveLinkTos, UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<StreamEventsSlice> ReadStreamEventsForwardAsync(string stream, long start, int count, bool resolveLinkTos,
		UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<StreamEventsSlice> ReadStreamEventsBackwardAsync(string stream, long start, int count, bool resolveLinkTos,
		UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<AllEventsSlice> ReadAllEventsForwardAsync(Position position, int maxCount, bool resolveLinkTos,
		UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<AllEventsSlice> FilteredReadAllEventsForwardAsync(Position position, int maxCount, bool resolveLinkTos, Filter filter,
		UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<AllEventsSlice> FilteredReadAllEventsForwardAsync(Position position, int maxCount, bool resolveLinkTos, Filter filter,
		int maxSearchWindow, UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<AllEventsSlice> ReadAllEventsBackwardAsync(Position position, int maxCount, bool resolveLinkTos,
		UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<AllEventsSlice> FilteredReadAllEventsBackwardAsync(Position position, int maxCount, bool resolveLinkTos, Filter filter,
		int maxSearchWindow, UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<AllEventsSlice> FilteredReadAllEventsBackwardAsync(Position position, int maxCount, bool resolveLinkTos, Filter filter,
		UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<EventStoreSubscription> SubscribeToStreamAsync(string stream, bool resolveLinkTos, Func<EventStoreSubscription, ResolvedEvent, Task> eventAppeared, Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
		UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public EventStoreStreamCatchUpSubscription SubscribeToStreamFrom(string stream, long? lastCheckpoint,
		CatchUpSubscriptionSettings settings, Func<EventStoreCatchUpSubscription, ResolvedEvent, Task> eventAppeared, Action<EventStoreCatchUpSubscription> liveProcessingStarted = null,
		Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null, UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<EventStoreSubscription> SubscribeToAllAsync(bool resolveLinkTos, Func<EventStoreSubscription, ResolvedEvent, Task> eventAppeared, Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
		UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<EventStoreSubscription> FilteredSubscribeToAllAsync(bool resolveLinkTos, Filter filter, Func<EventStoreSubscription, ResolvedEvent, Task> eventAppeared, Func<EventStoreSubscription, Position, Task> checkpointReached,
		int checkpointInterval, Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null, UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<EventStoreSubscription> FilteredSubscribeToAllAsync(bool resolveLinkTos, Filter filter, Func<EventStoreSubscription, ResolvedEvent, Task> eventAppeared,
		Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null, UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public EventStorePersistentSubscriptionBase ConnectToPersistentSubscription(string stream, string groupName,
		Func<EventStorePersistentSubscriptionBase, ResolvedEvent, int?, Task> eventAppeared, Action<EventStorePersistentSubscriptionBase, SubscriptionDropReason, Exception> subscriptionDropped = null, UserCredentials userCredentials = null, int bufferSize = 10,
		bool autoAck = true) {
		throw new NotImplementedException();
	}

	public Task<EventStorePersistentSubscriptionBase> ConnectToPersistentSubscriptionAsync(string stream, string groupName, Func<EventStorePersistentSubscriptionBase, ResolvedEvent, int?, Task> eventAppeared,
		Action<EventStorePersistentSubscriptionBase, SubscriptionDropReason, Exception> subscriptionDropped = null, UserCredentials userCredentials = null, int bufferSize = 10,
		bool autoAck = true) {
		throw new NotImplementedException();
	}

	public EventStoreAllCatchUpSubscription SubscribeToAllFrom(Position? lastCheckpoint, CatchUpSubscriptionSettings settings,
		Func<EventStoreCatchUpSubscription, ResolvedEvent, Task> eventAppeared, Action<EventStoreCatchUpSubscription> liveProcessingStarted = null, Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
		UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public EventStoreAllFilteredCatchUpSubscription FilteredSubscribeToAllFrom(Position? lastCheckpoint, Filter filter,
		CatchUpSubscriptionFilteredSettings settings, Func<EventStoreCatchUpSubscription, ResolvedEvent, Task> eventAppeared, Func<EventStoreCatchUpSubscription, Position, Task> checkpointReached,
		int checkpointIntervalMultiplier, Action<EventStoreCatchUpSubscription> liveProcessingStarted = null, Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
		UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public EventStoreAllFilteredCatchUpSubscription FilteredSubscribeToAllFrom(Position? lastCheckpoint, Filter filter,
		CatchUpSubscriptionFilteredSettings settings, Func<EventStoreCatchUpSubscription, ResolvedEvent, Task> eventAppeared, Action<EventStoreCatchUpSubscription> liveProcessingStarted = null,
		Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null, UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task UpdatePersistentSubscriptionAsync(string stream, string groupName, PersistentSubscriptionSettings settings,
		UserCredentials credentials) {
		throw new NotImplementedException();
	}

	public Task CreatePersistentSubscriptionAsync(string stream, string groupName, PersistentSubscriptionSettings settings,
		UserCredentials credentials) {
		throw new NotImplementedException();
	}

	public Task DeletePersistentSubscriptionAsync(string stream, string groupName, UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<WriteResult> SetStreamMetadataAsync(string stream, long expectedMetastreamVersion, StreamMetadata metadata,
		UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<WriteResult> SetStreamMetadataAsync(string stream, long expectedMetastreamVersion, byte[] metadata,
		UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<StreamMetadataResult> GetStreamMetadataAsync(string stream, UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<RawStreamMetadataResult> GetStreamMetadataAsRawBytesAsync(string stream, UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task SetSystemSettingsAsync(SystemSettings settings, UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public string ConnectionName { get; }
	public ConnectionSettings Settings { get; }
	public event EventHandler<ClientConnectionEventArgs> Connected;
	public event EventHandler<ClientConnectionEventArgs> Disconnected;
	public event EventHandler<ClientReconnectingEventArgs> Reconnecting;
	public event EventHandler<ClientClosedEventArgs> Closed;
	public event EventHandler<ClientErrorEventArgs> ErrorOccurred;
	public event EventHandler<ClientAuthenticationFailedEventArgs> AuthenticationFailed;
}
