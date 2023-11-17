extern alias GrpcClient;
extern alias GrpcClientStreams;
using EventStoreClientSettings = GrpcClient::EventStore.Client.EventStoreClientSettings;
using EventStoreStreamsClient = GrpcClientStreams::EventStore.Client.EventStoreClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Messages;
using GrpcClient::EventStore.Client;
using ConditionalWriteResult = EventStore.ClientAPI.ConditionalWriteResult;
using DeleteResult = EventStore.ClientAPI.DeleteResult;
using Direction = GrpcClientStreams::EventStore.Client.Direction;
using EventData = EventStore.ClientAPI.EventData;
using Position = EventStore.ClientAPI.Position;
using ResolvedEvent = GrpcClient::EventStore.Client.ResolvedEvent;
using StreamMessage = GrpcClientStreams::EventStore.Client.StreamMessage;
using StreamMetadata = EventStore.ClientAPI.StreamMetadata;
using StreamMetadataResult = EventStore.ClientAPI.StreamMetadataResult;
using StreamPosition = GrpcClient::EventStore.Client.StreamPosition;
using SystemSettings = EventStore.ClientAPI.SystemSettings;
using UserCredentials = EventStore.ClientAPI.SystemData.UserCredentials;

namespace EventStore.Core.Tests.ClientAPI.Helpers;

public class GrpcEventStoreConnection : IEventStoreConnection {
	private readonly IPEndPoint _endpoint;
	private EventStoreStreamsClient _streamsClient;

	public GrpcEventStoreConnection(IPEndPoint endpoint) {
		_endpoint = endpoint;
	}

	public void Dispose() {
		throw new NotImplementedException();
	}

	public Task ConnectAsync() {
		var setts = EventStoreClientSettings.Create($"esdb://{_endpoint.Address}:{_endpoint.Port}");
		_streamsClient = new EventStoreStreamsClient(setts);
		return Task.CompletedTask;
	}

	public void Close() {
		_streamsClient.Dispose();
		_streamsClient = null;
	}

	private GrpcClient::EventStore.Client.UserCredentials ConvertUC(UserCredentials creds) {
		return creds != null ? new GrpcClient::EventStore.Client.UserCredentials(creds.Username, creds.Password) : null;
	}

	private GrpcClient::EventStore.Client.EventData ConvertED(EventData data) {
		return new GrpcClient::EventStore.Client.EventData(Uuid.FromGuid(data.EventId), data.Type, data.Data, data.Metadata);
	}

	private ClientMessage.ResolvedIndexedEvent ConvertResolvedEvent(ResolvedEvent @event) {
		return null;
	}

	public Task<DeleteResult> DeleteStreamAsync(string stream, long expectedVersion, UserCredentials userCredentials = null) {
		return DeleteStreamAsync(stream, expectedVersion, false, userCredentials);
	}

	public async Task<DeleteResult> DeleteStreamAsync(string stream, long expectedVersion, bool hardDelete, UserCredentials userCredentials = null) {

		if (hardDelete) {
			var tombstoneResult = await _streamsClient.TombstoneAsync(stream, StreamRevision.FromInt64(expectedVersion), userCredentials:ConvertUC(userCredentials));
			return new DeleteResult(new Position((long)tombstoneResult.LogPosition.CommitPosition,
				(long)tombstoneResult.LogPosition.PreparePosition));
		}

		var deleteResult = await _streamsClient.DeleteAsync(stream, StreamRevision.FromInt64(expectedVersion),
			userCredentials: ConvertUC(userCredentials));

		return new DeleteResult(new Position((long)deleteResult.LogPosition.CommitPosition,
			(long)deleteResult.LogPosition.PreparePosition));
	}

	public Task<WriteResult> AppendToStreamAsync(string stream, long expectedVersion, params EventData[] events) {
		return AppendToStreamAsync(stream, expectedVersion, null, events);
	}

	public Task<WriteResult> AppendToStreamAsync(string stream, long expectedVersion, UserCredentials userCredentials,
		params EventData[] events) {
		return AppendToStreamAsync(stream, expectedVersion, events, userCredentials);
	}

	public async Task<WriteResult> AppendToStreamAsync(string stream, long expectedVersion, IEnumerable<EventData> events,
		UserCredentials userCredentials = null) {
		var result = await _streamsClient.AppendToStreamAsync(stream,
			StreamRevision.FromInt64(expectedVersion), events.Select(ConvertED), userCredentials: ConvertUC(userCredentials));

		return new WriteResult(result.NextExpectedStreamRevision.ToInt64(),
			new Position((long)result.LogPosition.CommitPosition, (long)result.LogPosition.PreparePosition));
	}

	public async Task<ConditionalWriteResult> ConditionalAppendToStreamAsync(string stream, long expectedVersion, IEnumerable<EventData> events,
		UserCredentials userCredentials = null) {
		var result = await AppendToStreamAsync(stream, expectedVersion, events, userCredentials);
		return new ConditionalWriteResult(result.NextExpectedVersion, result.LogPosition);
	}

	public Task<EventStoreTransaction> StartTransactionAsync(string stream, long expectedVersion, UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public EventStoreTransaction ContinueTransaction(long transactionId, UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public async Task<EventReadResult> ReadEventAsync(string stream, long eventNumber, bool resolveLinkTos, UserCredentials userCredentials = null) {
		var result = _streamsClient.ReadStreamAsync(Direction.Forwards, stream, StreamPosition.FromInt64(eventNumber),
			maxCount:1, resolveLinkTos: resolveLinkTos);

		await foreach (var message in result.Messages) {
			if (message is StreamMessage.Event @event) {
				return new EventReadResult(EventReadStatus.Success, stream, eventNumber,
					ConvertResolvedEvent(@event.ResolvedEvent));
			}
		}

		return new EventReadResult(EventReadStatus.NotFound, stream, eventNumber, null);
	}

	public async Task<StreamEventsSlice> ReadStreamEventsForwardAsync(string stream, long start, int count, bool resolveLinkTos,
		UserCredentials userCredentials = null) {
		var result = _streamsClient.ReadStreamAsync(Direction.Forwards, stream, StreamPosition.FromInt64(start),
			maxCount: count, resolveLinkTos: resolveLinkTos);

		var events = new List<ClientMessage.ResolvedIndexedEvent>();
		var lastEventNumber = -1L;
		var nextEventNumber = -1L;
		await foreach (var message in result.Messages) {
			switch (message)
			{
				case StreamMessage.Event @event:
					nextEventNumber = @event.ResolvedEvent.OriginalEventNumber.ToInt64() + 1;
					events.Add(ConvertResolvedEvent(@event.ResolvedEvent));
					break;

				case StreamMessage.LastStreamPosition last:
					lastEventNumber = last.StreamPosition.ToInt64();
					break;
			}
		}

		return new StreamEventsSlice(SliceReadStatus.Success, stream, start, ReadDirection.Forward, events.ToArray(),
			nextEventNumber, lastEventNumber, nextEventNumber >= lastEventNumber);
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
