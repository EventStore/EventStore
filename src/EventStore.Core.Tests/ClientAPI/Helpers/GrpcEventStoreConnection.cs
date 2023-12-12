extern alias GrpcClient;
extern alias GrpcClientStreams;
extern alias GrpcClientPersistent;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using GrpcClientStreams::EventStore.Client;
using EventStoreClientSettings = GrpcClient::EventStore.Client.EventStoreClientSettings;
using StreamDeletedException = GrpcClient::EventStore.Client.StreamDeletedException;
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
using SystemSettings = GrpcClientStreams::EventStore.Client.SystemSettings;
using SubscriptionDroppedReason = GrpcClient::EventStore.Client.SubscriptionDroppedReason;
using WEVE = GrpcClient::EventStore.Client.WrongExpectedVersionException;
using Uuid = GrpcClient::EventStore.Client.Uuid;
using EventStore.Common.Utils;
using FromAll = GrpcClient::EventStore.Client.FromAll;
using FromStream = GrpcClient::EventStore.Client.FromStream;
using PrefixFilterExpression = GrpcClient::EventStore.Client.PrefixFilterExpression;
using EventTypeFilter = GrpcClient::EventStore.Client.EventTypeFilter;
using StreamFilter = GrpcClient::EventStore.Client.StreamFilter;
using System.Text.RegularExpressions;

namespace EventStore.Core.Tests.ClientAPI.Helpers;

public class GrpcEventStoreConnection : IEventStoreClient {
	private readonly IPEndPoint _endpoint;
	private EventStoreStreamsClient _streamsClient;
	private EventStorePersistentSubscriptionsClient _psClient;
	private UserCredentials _defaultUserCredentials;

	public GrpcEventStoreConnection(IPEndPoint endpoint, UserCredentials defaultUserCredentials = null) {
		_endpoint = endpoint;
		_defaultUserCredentials = defaultUserCredentials;
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
		return FilteredSubscribeToAllFrom(
			null,
			filter,
			new CatchUpSubscriptionFilteredSettings(0, 10, resolveLinkTos: resoleLinkTos),
			eventAppeared,
			checkpointReached,
			checkpointInterval,
			subscriptionDropped: subscriptionDropped,
			userCredentials: userCredentials);
	}

	public async Task<AllEventsSliceNew> FilteredReadAllEventsForwardAsync(Position position, int maxCount, bool resolveLinkTos, IEventFilter filter,
		int maxSearchWindow, UserCredentials userCredentials = null) {
		if (maxCount == int.MaxValue)
			throw new ArgumentException("is equal to int.MaxValue", nameof(maxCount));

		// Internal code for disabling maxCount during test
		if (maxCount == -42) {
			maxCount = int.MaxValue;
		}

		if (maxSearchWindow <= 0)
			maxSearchWindow = 500;

		var events = new List<ResolvedEvent>();
		var nextPosition = position;
		var processedCount = 0;

		var result = _streamsClient.ReadAllAsync(Direction.Forwards, position, resolveLinkTos:resolveLinkTos,
			userCredentials: userCredentials);

		var endOfStream = true;
		await foreach (var message in result.Messages) {
			if (message is StreamMessage.Event @event) {
				nextPosition = @event.ResolvedEvent.OriginalPosition.Value;
				if (events.Count >= maxCount || processedCount >= maxSearchWindow) {
					endOfStream = false;
					break;
				}

				processedCount++;

				if (CandProcessEvent(ref filter, @event.ResolvedEvent))
					events.Add(@event.ResolvedEvent);
			}
		}

		return new AllEventsSliceNew(Direction.Forwards, nextPosition, endOfStream, events.ToArray());
	}

	public async Task<AllEventsSliceNew> FilteredReadAllEventsBackwardAsync(Position position, int maxCount, bool resolveLinkTos, IEventFilter filter,
		int maxSearchWindow, UserCredentials userCredentials = null) {
		if (maxCount == int.MaxValue)
			throw new ArgumentException("is equal to int.MaxValue", nameof(maxCount));

		if (maxSearchWindow <= 0)
			maxSearchWindow = 500;

		// Because in C#, we can't name loops, we have to resort to local variable to break from nested loops.
		var breakMainLoop = false;
		var events = new List<ResolvedEvent>();
		var nextPosition = position;
		var isEof = false;
		var processedCount = 0;

		while (events.Count < maxCount) {
			var result = _streamsClient.ReadAllAsync(Direction.Backwards, position, 500, resolveLinkTos,
				userCredentials: userCredentials);

			await foreach (var message in result.Messages) {
				switch (message) {
					case StreamMessage.Event @event:
						nextPosition = @event.ResolvedEvent.OriginalPosition!.Value;
						processedCount++;

						if (CandProcessEvent(ref filter, @event.ResolvedEvent))
							events.Add(@event.ResolvedEvent);

						breakMainLoop = events.Count >= maxCount || processedCount >= maxSearchWindow;
						break;
				}

				if (breakMainLoop)
					break;
			}

			isEof = nextPosition <= Position.Start || position == nextPosition;
			if (breakMainLoop || isEof)
				break;

			position = nextPosition;
		}

		return new AllEventsSliceNew(Direction.Backwards, nextPosition, isEof, events.ToArray());
	}

	private static bool CandProcessEvent(ref IEventFilter filter, ResolvedEvent @event) {
		if (filter == null)
			return true;

		var canProcess = false;
		var isStreamNameBased = false;
		switch (filter) {
			case StreamFilter:
				isStreamNameBased = true;
				break;
			case EventTypeFilter:
				isStreamNameBased = false;
				break;
		}

		if (filter.Prefixes.IsNotEmpty()) {
			foreach (var prefix in filter.Prefixes) {
				if (isStreamNameBased) {
					if (@event.OriginalEvent.EventStreamId.StartsWith(prefix)) {
						canProcess = true;
						break;
					}
				} else {
					if (@event.OriginalEvent.EventType.StartsWith(prefix)) {
						canProcess = true;
						break;
					}
				}
			}
		} else {
			var regex = new Regex(filter.Regex.ToString());
			if (isStreamNameBased) {
				canProcess = regex.Match(@event.OriginalEvent.EventStreamId).Success;
			} else {
				canProcess = regex.Match(@event.OriginalEvent.EventType).Success;
			}
		}
		
		return canProcess;
	}

	public async Task<StreamSubscription> FilteredSubscribeToAllFrom(Position? lastCheckpoint, IEventFilter filter,
		CatchUpSubscriptionFilteredSettings settings, Func<StreamSubscription, ResolvedEvent, Task> eventAppeared, Func<StreamSubscription, Position, Task> checkpointReached,
		int checkpointIntervalMultiplier, Action<StreamSubscription> liveProcessingStarted = null, Action<StreamSubscription, SubscriptionDroppedReason, Exception> subscriptionDropped = null,
		UserCredentials userCredentials = null) {

		var starting = lastCheckpoint ?? Position.End;
		var from = FromAll.End;

		if (starting != Position.End) {
			var slice = await FilteredReadAllEventsForwardAsync(starting, -42, settings.ResolveLinkTos, filter, 500, userCredentials: userCredentials);
			from = FromAll.After(slice.NextPosition);

			foreach (var @event in slice.Events)
				// Hopefully, nobody is using the subcription handle this early.
				// Worst case scenario, I have to create another StreamSubscription sham type.
				await eventAppeared(null, @event);
		}

		var options = new SubscriptionFilterOptions(
			filter,
			(uint)checkpointIntervalMultiplier,
			(s, p, _) => checkpointReached(s, p));

		var sub = await _streamsClient.SubscribeToAllAsync(
			from,
			(s,e, _) => eventAppeared(s, e),
			resolveLinkTos: settings.ResolveLinkTos,
			filterOptions: options,
			subscriptionDropped: subscriptionDropped,
			userCredentials: userCredentials);

		liveProcessingStarted?.Invoke(sub);

		return sub;
	}

	public Task<StreamMetadataResult> GetStreamMetadataAsync(string stream, UserCredentials userCredentials = null) {
		return _streamsClient.GetStreamMetadataAsync(stream, userCredentials: userCredentials);
	}

	public Task<StreamMetadataResult> GetStreamMetadataAsRawBytesAsync(string stream, UserCredentials userCredentials = null) {
		return GetStreamMetadataAsync(stream, userCredentials);
	}

	public Task<StreamSubscription> SubscribeToAllFrom(Position? lastCheckpoint, CatchUpSubscriptionSettings settings, Func<StreamSubscription, ResolvedEvent, Task> eventAppeared,
		Action<StreamSubscription> liveProcessingStarted = null, Action<StreamSubscription, SubscriptionDroppedReason, Exception> subscriptionDropped = null, UserCredentials userCredentials = null) {
		return FilteredSubscribeToAllFrom(lastCheckpoint, null, CatchUpSubscriptionFilteredSettings.FromSettings(settings), eventAppeared, null, 1, liveProcessingStarted, subscriptionDropped, userCredentials);
	}

	public Task<StreamSubscription> SubscribeToAllAsync(bool resolveLinkTos, Func<StreamSubscription, ResolvedEvent, Task> eventAppeared, Action<StreamSubscription, SubscriptionDroppedReason, Exception> subscriptionDropped = null,
		UserCredentials userCredentials = null) {
		return _streamsClient.SubscribeToAllAsync(
			FromAll.End, (s, e, _) => eventAppeared(s, e),
			resolveLinkTos: resolveLinkTos,
			subscriptionDropped: subscriptionDropped,
			userCredentials: userCredentials);
	}

	public Task<StreamSubscription> SubscribeToStreamFrom(string stream, long? lastCheckpoint, CatchUpSubscriptionSettings settings,
		Func<StreamSubscription, ResolvedEvent, Task> eventAppeared, Action<StreamPosition> liveProcessingStarted = null, Action<StreamSubscription, SubscriptionDroppedReason, Exception> subscriptionDropped = null,
		UserCredentials userCredentials = null) {
		var start = lastCheckpoint.HasValue ? FromStream.After(StreamPosition.FromInt64(lastCheckpoint.Value)) : FromStream.End;
		return _streamsClient.SubscribeToStreamAsync(
			stream,
			start,
			(s, e, _) => eventAppeared(s, e),
			subscriptionDropped: subscriptionDropped,
			resolveLinkTos: settings.ResolveLinkTos,
			userCredentials: userCredentials);
	}

	public Task<StreamSubscription> SubscribeToStreamAsync(string stream, bool resolveLinkTos,
		Func<StreamSubscription, ResolvedEvent, Task> eventAppeared,
		Action<StreamSubscription, SubscriptionDroppedReason, Exception> subscriptionDropped = null,
		UserCredentials liveProcessingStarted = null, UserCredentials userCredentials = null) {
		return _streamsClient.SubscribeToStreamAsync(
			stream,
			FromStream.End,
			(s, e, _) => eventAppeared(s, e),
			resolveLinkTos: resolveLinkTos,
			subscriptionDropped: subscriptionDropped,
			userCredentials: userCredentials);
	}

	public Task DeletePersistentSubscriptionAsync(string stream, string group, UserCredentials userCredentials = null) {
		return _psClient.DeleteToStreamAsync(stream, group, userCredentials: userCredentials);
	}

	public Task UpdatePersistentSubscriptionAsync(string stream, string group, PersistentSubscriptionSettings settings,
		UserCredentials userCredentials = null) {
		return _psClient.UpdateToStreamAsync(stream, group, settings, userCredentials: userCredentials);
	}

	public Task ConnectAsync() {
		var setts = EventStoreClientSettings.Create($"esdb://{_endpoint.Address}:{_endpoint.Port}?tlsVerifyCert=false");
		if (_defaultUserCredentials != null)
			setts.DefaultCredentials = _defaultUserCredentials;

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

		try {
			var result = version.IsState
				? await _streamsClient.AppendToStreamAsync(stream, version.State!.Value, events,
					userCredentials: userCredentials)
				: await _streamsClient.AppendToStreamAsync(stream, version.Revision!.Value, events,
					userCredentials: userCredentials);


			return new WriteResult(result.NextExpectedStreamRevision.ToInt64(), result.LogPosition);
		} catch (WEVE e) {
			throw new WrongExpectedVersionException(version.Raw, e.ActualVersion.GetValueOrDefault(-1));
		}
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
		try {
			var result = _streamsClient.ReadStreamAsync(Direction.Forwards, stream, StreamPosition.FromInt64(start),
				maxCount: count, resolveLinkTos: resolveLinkTos, userCredentials: userCredentials);

			var events = new List<ResolvedEvent>();
			var lastStreamEventNumber = -1L;
			var lastEventNumber = -1L;
			var nextEventNumber = -1L;
			await foreach (var message in result.Messages) {
				switch (message) {
					case StreamMessage.Event @event:
						lastEventNumber = @event.ResolvedEvent.OriginalEventNumber.ToInt64();
						nextEventNumber = lastEventNumber + 1;
						events.Add(@event.ResolvedEvent);
						break;

					case StreamMessage.LastStreamPosition last:
						lastStreamEventNumber = last.StreamPosition.ToInt64();
						break;
				}
			}

			return new StreamEventsSliceNew(stream, Direction.Forwards, start, nextEventNumber,
				lastStreamEventNumber, lastEventNumber >= lastStreamEventNumber, events.ToArray());
		} catch (StreamDeletedException) {
			return new StreamEventsSliceNew(SliceReadStatus.StreamDeleted);
		}
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

	public Task<AllEventsSliceNew> ReadAllEventsForwardAsync(Position position, int maxCount, bool resolveLinkTos,
		UserCredentials userCredentials = null) {
		return FilteredReadAllEventsForwardAsync(position, maxCount, resolveLinkTos, null, 0, userCredentials);
	}

	public Task<AllEventsSliceNew> ReadAllEventsBackwardAsync(Position position, int maxCount, bool resolveLinkTos,
		UserCredentials userCredentials = null) {
		return FilteredReadAllEventsBackwardAsync(position, maxCount, resolveLinkTos, null, 1, userCredentials);
	}

	public Task CreatePersistentSubscriptionAsync(string stream, string groupName, GrpcClientPersistent::EventStore.Client.PersistentSubscriptionSettings settings,
		UserCredentials userCredentials = null) {
		return _psClient.CreateToStreamAsync(stream, groupName, settings, userCredentials: userCredentials);
	}

	public async Task SetSystemSettingsAsync(SystemSettings settings, UserCredentials userCredentials = null) {
		var @event = new EventData(Uuid.NewUuid(), "$settings", settings.ToJsonBytes());
		await _streamsClient.AppendToStreamAsync("$settings", StreamState.Any, new[] {@event}, userCredentials: userCredentials);
	}

	public Task Close() {
		return Task.CompletedTask;
	}

	record StreamStateOrRevision(StreamState? State, StreamRevision? Revision) {
		public bool IsState => State != null;
		public long Raw => IsState ? State!.Value.ToInt64() : Revision!.Value.ToInt64();
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
