extern alias GrpcClient;
extern alias GrpcClientStreams;
extern alias GrpcClientPersistent;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using GrpcClientStreams::EventStore.Client;
using EventStoreClientSettings = GrpcClient::EventStore.Client.EventStoreClientSettings;
using EventStoreStreamsClient = GrpcClientStreams::EventStore.Client.EventStoreClient;
using StreamRevision = GrpcClient::EventStore.Client.StreamRevision;
using UserCredentials = GrpcClient::EventStore.Client.UserCredentials;
using EventData = GrpcClient::EventStore.Client.EventData;
using EventStorePersistentSubscriptionsClient = GrpcClientPersistent::EventStore.Client.EventStorePersistentSubscriptionsClient;
using IEventFilter = GrpcClient::EventStore.Client.IEventFilter;
using PersistentSubscription = GrpcClientPersistent::EventStore.Client.PersistentSubscription;
using PersistentSubscriptionSettings = GrpcClientPersistent::EventStore.Client.PersistentSubscriptionSettings;
using Position = GrpcClient::EventStore.Client.Position;
using ResolvedEvent = GrpcClient::EventStore.Client.ResolvedEvent;
using StreamPosition = GrpcClient::EventStore.Client.StreamPosition;
using StreamState = GrpcClient::EventStore.Client.StreamState;
using SubscriptionDroppedReason = GrpcClient::EventStore.Client.SubscriptionDroppedReason;

namespace EventStore.Core.Tests.ClientAPI.Helpers;

public class GrpcEventStoreConnection : IEventStoreClient {
	private readonly IPEndPoint _endpoint;
	private EventStoreStreamsClient _streamsClient;
	private EventStorePersistentSubscriptionsClient _psClient;

	public GrpcEventStoreConnection(IPEndPoint endpoint) {
		_endpoint = endpoint;
	}

	public void Dispose() {
		_streamsClient.Dispose();
		_psClient.Dispose();
		_streamsClient = null;
		_psClient = null;
	}

	public Task<PersistentSubscription> ConnectToPersistentSubscription(string stream, string groupName, Func<PersistentSubscription, ResolvedEvent, int?, Task> eventAppeared,
		Action<PersistentSubscription, SubscriptionDroppedReason, Exception> subscriptionDropped = null, UserCredentials userCredentials = null, int bufferSize = 10,
		bool autoAck = true) {
		return _psClient.SubscribeToStreamAsync(
			stream,
			groupName, async (ps, @event, num, token) => {
				await eventAppeared(ps, @event, num);
				if (autoAck)
					await ps.Ack(@event);
			}, subscriptionDropped, userCredentials,
			bufferSize);
	}

	public Task<StreamSubscription> FilteredSubscribeToAllFrom(bool resoleLinkTos, IEventFilter filter, Func<StreamSubscription, ResolvedEvent, Task> eventAppeared,
		Func<StreamSubscription, Position, Task> checkpointReached, int checkpointInterval, Action<StreamSubscription, SubscriptionDroppedReason, Exception> subscriptionDropped = null,
		UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<StreamSubscription> FilteredSubscribeToAllAsync(bool resoleLinkTos, IEventFilter filter, Func<StreamSubscription, ResolvedEvent, Task> eventAppeared, Func<StreamSubscription, Position, Task> checkpointReached,
		int checkpointInterval, Action<StreamSubscription, SubscriptionDroppedReason, Exception> subscriptionDropped = null, UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<AllEventsSliceNew> FilteredReadAllEventsForwardAsync(Position position, int maxCount, bool resolveLinkTos, IEventFilter filter,
		int maxSearchWindow, UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<AllEventsSliceNew> FilteredReadAllEventsBackwardAsync(Position position, int maxCount, bool resolveLinkTos, IEventFilter filter,
		int maxSearchWindow, UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<StreamSubscription> FilteredSubscribeToAllFrom(Position? lastCheckpoint, IEventFilter filter,
		CatchUpSubscriptionFilteredSettings settings, Func<StreamSubscription, ResolvedEvent, Task> eventAppeared, Func<StreamSubscription, Position, Task> checkpointReached,
		int checkpointIntervalMultiplier, Action<StreamSubscription> liveProcessingStarted = null, Action<StreamSubscription, SubscriptionDroppedReason, Exception> subscriptionDropped = null,
		UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<StreamMetadataResult> GetStreamMetadataAsync(string stream, UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<StreamMetadataResult> GetStreamMetadataAsRawBytesAsync(string stream, UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<StreamSubscription> SubscribeToAllFrom(Position? lastCheckpoint, CatchUpSubscriptionSettings settings, Func<StreamSubscription, ResolvedEvent, Task> eventAppeared,
		Action<StreamSubscription> liveProcessingStarted = null, Action<StreamSubscription, SubscriptionDroppedReason, Exception> subscriptionDropped = null, UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<StreamSubscription> SubscribeToAllAsync(bool resolveLinkTos, Func<StreamSubscription, ResolvedEvent, Task> eventAppeared, Action<StreamSubscription, SubscriptionDroppedReason, Exception> subscriptionDropped = null,
		UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<StreamSubscription> SubscribeToStreamFrom(string stream, long? lastCheckpoint, CatchUpSubscriptionSettings settings,
		Func<StreamSubscription, ResolvedEvent, Task> eventAppeared, Action<StreamPosition> liveProcessingStarted = null, Action<StreamSubscription, SubscriptionDroppedReason, Exception> subscriptionDropped = null,
		UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<StreamSubscription> SubscribeToStreamAsync(string stream, bool resolveLinkTos, Func<StreamSubscription, ResolvedEvent, Task> eventAppeared, Action<StreamSubscription, SubscriptionDroppedReason, Exception> subscriptionDropped = null,
		Action<StreamPosition> liveProcessingStarted = null, UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task DeletePersistentSubscriptionAsync(string stream, string group, UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task UpdatePersistentSubscriptionAsync(string stream, string group, PersistentSubscriptionSettings settings,
		UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task ConnectAsync() {
		var setts = EventStoreClientSettings.Create($"esdb://{_endpoint.Address}:{_endpoint.Port}");
		_streamsClient = new EventStoreStreamsClient(setts);
		_psClient = new EventStorePersistentSubscriptionsClient(setts);
		return Task.CompletedTask;
	}

	public async Task<DeleteResult> DeleteStreamAsync(string stream, long expectedVersion, bool hardDelete = false, UserCredentials userCredentials = null) {
		var version = FromUInt64(expectedVersion);
		if (hardDelete) {
			var tombstoneResult = version.IsState
				? await _streamsClient.TombstoneAsync(stream, version.State!.Value, userCredentials: userCredentials)
				: await _streamsClient.TombstoneAsync(stream, version.Revision!.Value, userCredentials: userCredentials);
			return new DeleteResult(tombstoneResult.LogPosition);
		}

		var deleteResult = version.IsState
			? await _streamsClient.DeleteAsync(stream, version.State!.Value, userCredentials: userCredentials)
			: await _streamsClient.DeleteAsync(stream, version.Revision!.Value, userCredentials: userCredentials);

		return new DeleteResult(deleteResult.LogPosition);
	}

	public async Task<WriteResult> AppendToStreamAsync(string stream, long expectedVersion, IEnumerable<EventData> events,
		UserCredentials userCredentials = null) {
		var version = FromUInt64(expectedVersion);
		var result = version.IsState
			? await _streamsClient.AppendToStreamAsync(stream, version.State!.Value, events, userCredentials: userCredentials)
			: await _streamsClient.AppendToStreamAsync(stream, version.Revision!.Value, events, userCredentials: userCredentials);

		return new WriteResult(result.NextExpectedStreamRevision.ToInt64(), result.LogPosition);
	}

	public async Task<EventReadResultNew> ReadEventAsync(string stream, long eventNumber, bool resolveLinkTos, UserCredentials userCredentials = null) {
		var result = _streamsClient.ReadStreamAsync(Direction.Forwards, stream, StreamPosition.FromInt64(eventNumber),
			maxCount:1, resolveLinkTos: resolveLinkTos);

		await foreach (var message in result.Messages) {
			if (message is StreamMessage.Event @event) {
				return new EventReadResultNew(EventReadStatus.Success, stream, eventNumber,
					@event.ResolvedEvent);
			}
		}

		return new EventReadResultNew(EventReadStatus.NotFound, stream, eventNumber, null);
	}

	public async Task<WriteResult> SetStreamMetadataAsync(string stream, long expectedMetaStreamVersion, StreamMetadata metadata,
		UserCredentials userCredentials = null) {
		var version = FromUInt64(expectedMetaStreamVersion);
		var result = version.IsState
			? await _streamsClient.SetStreamMetadataAsync(stream, version.State!.Value, metadata, userCredentials: userCredentials)
			: await _streamsClient.SetStreamMetadataAsync(stream, version.Revision!.Value, metadata, userCredentials: userCredentials);

		return new WriteResult(result.NextExpectedStreamRevision.ToInt64(), result.LogPosition);
	}

	public async Task<StreamEventsSliceNew> ReadStreamEventsForwardAsync(string stream, long start, int count, bool resolveLinkTos,
		UserCredentials userCredentials = null) {
		var result = _streamsClient.ReadStreamAsync(Direction.Forwards, stream, StreamPosition.FromInt64(start),
			maxCount: count, resolveLinkTos: resolveLinkTos, userCredentials: userCredentials);

		var events = new List<ResolvedEvent>();
		var lastEventNumber = -1L;
		var nextEventNumber = -1L;
		await foreach (var message in result.Messages) {
			switch (message)
			{
				case StreamMessage.Event @event:
					nextEventNumber = @event.ResolvedEvent.OriginalEventNumber.ToInt64() + 1;
					events.Add(@event.ResolvedEvent);
					break;

				case StreamMessage.LastStreamPosition last:
					lastEventNumber = last.StreamPosition.ToInt64();
					break;
			}
		}

		return new StreamEventsSliceNew(stream, Direction.Forwards, start, nextEventNumber,
			lastEventNumber,nextEventNumber >= lastEventNumber, events.ToArray());
	}

	public async Task<StreamEventsSliceNew> ReadStreamEventsBackwardAsync(string stream, long start, int count, bool resolveLinkTos,
		UserCredentials userCredentials = null) {
		var result = _streamsClient.ReadStreamAsync(Direction.Backwards, stream, StreamPosition.FromInt64(start),
			maxCount: count, resolveLinkTos: resolveLinkTos, userCredentials: userCredentials);

		var events = new List<ResolvedEvent>();
		var lastEventNumber = -1L;
		var nextEventNumber = -1L;
		await foreach (var message in result.Messages) {
			switch (message)
			{
				case StreamMessage.Event @event:
					nextEventNumber = @event.ResolvedEvent.OriginalEventNumber.ToInt64() - 1;
					events.Add(@event.ResolvedEvent);
					break;

				case StreamMessage.FirstStreamPosition first:
					lastEventNumber = first.StreamPosition.ToInt64();
					break;
			}
		}

		return new StreamEventsSliceNew(stream, Direction.Backwards, start, nextEventNumber,
			lastEventNumber,nextEventNumber <= lastEventNumber, events.ToArray());
	}

	public async Task<AllEventsSliceNew> ReadAllEventsForwardAsync(Position position, int maxCount, bool resolveLinkTos,
		UserCredentials userCredentials = null) {
		var result = _streamsClient.ReadAllAsync(Direction.Forwards, position, maxCount, resolveLinkTos,
			userCredentials: userCredentials);

		var events = new List<ResolvedEvent>();
		var nextPosition = Position.Start;
		var lastPosition = Position.Start;
		await foreach (var message in result.Messages) {
			switch (message)
			{
				case StreamMessage.Event @event:
					nextPosition = @event.ResolvedEvent.OriginalPosition!.Value;
					events.Add(@event.ResolvedEvent);
					break;

				case StreamMessage.LastAllStreamPosition last:
					lastPosition = last.Position;
					break;
			}
		}

		return new AllEventsSliceNew(Direction.Forwards, nextPosition, nextPosition >= lastPosition, events.ToArray());
	}

	public async Task<AllEventsSliceNew> ReadAllEventsBackwardAsync(Position position, int maxCount, bool resolveLinkTos,
		UserCredentials userCredentials = null) {
		var result = _streamsClient.ReadAllAsync(Direction.Backwards, position, maxCount, resolveLinkTos,
			userCredentials: userCredentials);

		var events = new List<ResolvedEvent>();
		var nextPosition = Position.End;
		var lastPosition = Position.Start;
		await foreach (var message in result.Messages) {
			switch (message)
			{
				case StreamMessage.Event @event:
					nextPosition = @event.ResolvedEvent.OriginalPosition!.Value;
					events.Add(@event.ResolvedEvent);
					break;
			}
		}

		return new AllEventsSliceNew(Direction.Backwards, nextPosition, nextPosition <= lastPosition, events.ToArray());
	}

	public Task CreatePersistentSubscriptionAsync(string stream, string groupName, GrpcClientPersistent::EventStore.Client.PersistentSubscriptionSettings settings,
		UserCredentials userCredentials = null) {
		return _psClient.CreateToStreamAsync(stream, groupName, settings, userCredentials: userCredentials);
	}

	public Task Close() {
		return Task.CompletedTask;
	}

	record StreamStateOrRevision(StreamState? State, StreamRevision? Revision) {
		public bool IsState => State != null;
	}

	StreamStateOrRevision FromUInt64(long value) {
		StreamState? state = null;
		StreamRevision? revision = null;

		switch (value) {
			case -2:
				state = StreamState.Any;
				break;
			case -1:
				state = StreamState.NoStream;
				break;
			case -4:
				state = StreamState.StreamExists;
				break;
			default:
				revision = StreamRevision.FromInt64(value);
				break;
		}

		return new StreamStateOrRevision(state, revision);
	}
}
