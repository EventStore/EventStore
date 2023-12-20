extern alias GrpcClient;
extern alias GrpcClientStreams;
extern alias GrpcClientPersistent;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
			Position.Start,
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

		var isEndOfStream = true;
		await using var messages = result.Messages.GetAsyncEnumerator();
		while (await messages.MoveNextAsync()) {
			if (messages.Current is not StreamMessage.Event @event)
				continue;

			nextPosition = @event.ResolvedEvent.OriginalPosition!.Value;
			processedCount++;

			if (CanProcessEvent(ref filter, @event.ResolvedEvent))
				events.Add(@event.ResolvedEvent);

			if (events.Count >= maxCount || processedCount >= maxSearchWindow)
				break;
		}

		while (await messages.MoveNextAsync()) {
			if (messages.Current is not StreamMessage.Event @event)
				continue;

			nextPosition = @event.ResolvedEvent.OriginalPosition!.Value;
			isEndOfStream = false;
			break;
		}

		return new AllEventsSliceNew(Direction.Forwards, nextPosition, isEndOfStream, events.ToArray());
	}

	public async Task<AllEventsSliceNew> FilteredReadAllEventsBackwardAsync(Position position, int maxCount,
		bool resolveLinkTos, IEventFilter filter,
		int maxSearchWindow, UserCredentials userCredentials = null) {
		if (maxCount == int.MaxValue)
			throw new ArgumentException("is equal to int.MaxValue", nameof(maxCount));

		if (maxSearchWindow <= 0)
			maxSearchWindow = 500;

		var events = new List<ResolvedEvent>();
		var nextPosition = position;
		var processedCount = 0;
		var isEndOfStream = true;

		var result = _streamsClient.ReadAllAsync(Direction.Backwards, position, resolveLinkTos: resolveLinkTos,
			userCredentials: userCredentials);

		await using var messages = result.Messages.GetAsyncEnumerator();
		while (await messages.MoveNextAsync()) {
			if (messages.Current is not StreamMessage.Event @event)
				continue;

			nextPosition = @event.ResolvedEvent.OriginalPosition!.Value;
			Debug.WriteLine($"Count: {events.Count}");
			Debug.WriteLine($"processCount: {processedCount} -> {processedCount + 1}");
			processedCount++;

			if (CanProcessEvent(ref filter, @event.ResolvedEvent))
				events.Add(@event.ResolvedEvent);

			if (events.Count >= maxCount || processedCount >= maxSearchWindow)
				break;
		}

		Debug.WriteLine("Completed");
		while (await messages.MoveNextAsync()) {
			if (messages.Current is not StreamMessage.Event @event)
				continue;

			isEndOfStream = false;
			break;
		}

		return new AllEventsSliceNew(Direction.Backwards, nextPosition, isEndOfStream, events.ToArray());
	}

	private static bool CanProcessEvent(ref IEventFilter filter, ResolvedEvent @event) {
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

		var sub = new StreamSubscription();
		var token = sub.CancellationTokenSource.Token;
		var starting = lastCheckpoint ?? Position.Start;
		var from = FromAll.End;
		var checkpointInterval = checkpointIntervalMultiplier * settings.MaxSearchWindow;
		var processedCount = 0;

		if (starting != Position.End) {
			var nextPosition = Position.Start;
			var result = _streamsClient.ReadAllAsync(
				Direction.Forwards,
				starting,
				resolveLinkTos: settings.ResolveLinkTos,
				userCredentials: userCredentials);

			await foreach (var message in result.Messages.WithCancellation(token)) {
				if (token.IsCancellationRequested)
					return sub;

				if (message is not StreamMessage.Event @event)
					continue;

				processedCount++;
				try {
					if (CanProcessEvent(ref filter, @event.ResolvedEvent))
						await eventAppeared(sub, @event.ResolvedEvent);

					if (processedCount % checkpointInterval == 0)
						await checkpointReached(sub, @event.ResolvedEvent.OriginalPosition!.Value);

					nextPosition = @event.ResolvedEvent.OriginalPosition!.Value;
				} catch (Exception ex) {
					sub.ReportSubscriberError(ex);
					return sub;
				}
			}

			from = FromAll.After(nextPosition);
		}
		SubscriptionFilterOptions options = null;

		if (filter != null) {
			options = new SubscriptionFilterOptions(
				filter,
				(uint)checkpointIntervalMultiplier,
				(s, p, _) => checkpointReached(sub, p));
		}

		var internalSub = await _streamsClient.SubscribeToAllAsync(
			from,
			(_,e, __) =>  eventAppeared(sub, e),
			resolveLinkTos: settings.ResolveLinkTos,
			filterOptions: options,
			cancellationToken: token,
			subscriptionDropped: (_, r, ex) => sub.ReportDropped(r, ex),
			userCredentials: userCredentials);

		liveProcessingStarted?.Invoke(sub);

		sub.Internal = internalSub;

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
		return FilteredSubscribeToAllFrom(lastCheckpoint, null, CatchUpSubscriptionFilteredSettings.FromSettings(settings), eventAppeared, (_, _) => Task.CompletedTask, 1, liveProcessingStarted, subscriptionDropped, userCredentials);
	}

	public Task<StreamSubscription> SubscribeToAllAsync(bool resolveLinkTos, Func<StreamSubscription, ResolvedEvent, Task> eventAppeared, Action<StreamSubscription, SubscriptionDroppedReason, Exception> subscriptionDropped = null,
		UserCredentials userCredentials = null) {
		var settings = new CatchUpSubscriptionFilteredSettings(500, 500, false, resolveLinkTos);
		return FilteredSubscribeToAllFrom(
			Position.Start,
			null,
			settings,
			eventAppeared, (_, __) => Task.CompletedTask,
			500,
			subscriptionDropped: subscriptionDropped, userCredentials: userCredentials);
	}

	public async Task<StreamSubscription> SubscribeToStreamFrom(string stream, long? lastCheckpoint, CatchUpSubscriptionSettings settings,
		Func<StreamSubscription, ResolvedEvent, Task> eventAppeared, Action<StreamPosition> liveProcessingStarted = null, Action<StreamSubscription, SubscriptionDroppedReason, Exception> subscriptionDropped = null,
		UserCredentials userCredentials = null) {
		var sub = new StreamSubscription {
			SubscriptionDropped = subscriptionDropped
		};
		var token = sub.CancellationTokenSource.Token;
		var nextRevision = lastCheckpoint ?? 0;

		var result = _streamsClient.ReadStreamAsync(
			Direction.Forwards,
			stream, StreamPosition.FromInt64(nextRevision),
			resolveLinkTos: settings.ResolveLinkTos,
			userCredentials: userCredentials);

		await foreach (var message in result.Messages) {
			if (token.IsCancellationRequested)
				return sub;

			if (message is not StreamMessage.Event @event)
				continue;

			try {
				await eventAppeared(sub, @event.ResolvedEvent);
				nextRevision = @event.ResolvedEvent.OriginalEventNumber.ToInt64();
			} catch (Exception ex) {
				sub.ReportSubscriberError(ex);
				return sub;
			}
		}

		var internalSub = await _streamsClient.SubscribeToStreamAsync(
			stream,
			FromStream.After(StreamPosition.FromInt64(nextRevision)),
			(s, e, _) => eventAppeared(sub, e),
			subscriptionDropped: (_, r, ex) => sub.ReportDropped(r, ex),
			cancellationToken: token,
			resolveLinkTos: settings.ResolveLinkTos,
			userCredentials: userCredentials);

		sub.Internal = internalSub;

		liveProcessingStarted?.Invoke(StreamPosition.FromInt64(nextRevision));

		return sub;
	}

	public Task<StreamSubscription> SubscribeToStreamAsync(string stream, bool resolveLinkTos,
		Func<StreamSubscription, ResolvedEvent, Task> eventAppeared,
		Action<StreamSubscription, SubscriptionDroppedReason, Exception> subscriptionDropped = null,
		Action<StreamPosition> liveProcessingStarted = null, UserCredentials userCredentials = null) {
		return SubscribeToStreamFrom(
			stream,
			null,
			new CatchUpSubscriptionSettings(500, 500, false, resolveLinkTos),
			eventAppeared,
			liveProcessingStarted,
			subscriptionDropped,
			userCredentials);
	}

	public Task DeletePersistentSubscriptionAsync(string stream, string group, UserCredentials userCredentials = null) {
		return _psClient.DeleteToStreamAsync(stream, group, userCredentials: userCredentials);
	}

	public Task UpdatePersistentSubscriptionAsync(string stream, string group, PersistentSubscriptionSettings settings,
		UserCredentials userCredentials = null) {
		return _psClient.UpdateToStreamAsync(stream, group, settings, userCredentials: userCredentials);
	}

	public Task ConnectAsync() {
		var setts = EventStoreClientSettings.Create($"esdb://{_endpoint.Address}:{_endpoint.Port}?tlsVerifyCert=false&defaultDeadline=60000");
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
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(eventNumber, -2, nameof(eventNumber));
		
		if (string.IsNullOrEmpty(stream))
			throw new ArgumentNullException(nameof(stream));

		try {
			var result = eventNumber == -1
				? _streamsClient.ReadStreamAsync(Direction.Backwards, stream, StreamPosition.End,
					maxCount: 1, resolveLinkTos: resolveLinkTos, userCredentials: userCredentials)
				: _streamsClient.ReadStreamAsync(Direction.Forwards, stream, StreamPosition.FromInt64(eventNumber),
					maxCount: 1, resolveLinkTos: resolveLinkTos, userCredentials: userCredentials);

			await foreach (var message in result.Messages) {
				if (message is StreamMessage.Event @event) {
					return new EventReadResultNew(EventReadStatus.Success, stream, eventNumber,
						@event.ResolvedEvent);
				}
			}

			var status = eventNumber == -1 ? EventReadStatus.NoStream : EventReadStatus.NotFound;
			return new EventReadResultNew(status, stream, eventNumber, null);
		} catch (StreamDeletedException) {
			return new EventReadResultNew(EventReadStatus.StreamDeleted, stream, eventNumber);
		}
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
			var lastStreamEventNumber = -1L;
			var result = _streamsClient.ReadStreamAsync(Direction.Forwards, stream, StreamPosition.FromInt64(start),
				maxCount: 1, resolveLinkTos: resolveLinkTos, userCredentials: userCredentials);

			await foreach(var message in result.Messages) {
				switch (message) {
					case StreamMessage.LastStreamPosition lastPos:
						lastStreamEventNumber = lastPos.StreamPosition.ToInt64();
						break;

					case StreamMessage.NotFound _:
						return new StreamEventsSliceNew(SliceReadStatus.StreamNotFound);
				}
			}

			result = _streamsClient.ReadStreamAsync(Direction.Forwards, stream, StreamPosition.FromInt64(start),
				resolveLinkTos: resolveLinkTos, userCredentials: userCredentials);

			var events = new List<ResolvedEvent>();
			var nextEventNumber = -1L;
			var isEndOfStream = true;
			await foreach (var message in result.Messages) {
				if (message is StreamMessage.Event @event) {
					nextEventNumber = @event.ResolvedEvent.OriginalEventNumber.ToInt64();

					if (events.Count >= count || OutOfRangeEvent(start, count, @event.ResolvedEvent)) {
						isEndOfStream = false;
						break;
					}

					events.Add(@event.ResolvedEvent);
				}
			}

			return new StreamEventsSliceNew(stream, Direction.Forwards, start, nextEventNumber,
				lastStreamEventNumber, isEndOfStream, events.ToArray());
		} catch (StreamDeletedException) {
			return new StreamEventsSliceNew(SliceReadStatus.StreamDeleted);
		}
	}

	private static bool OutOfRangeEvent(long start, long count, ResolvedEvent @event) {
		return !(@event.OriginalEventNumber.ToInt64() >= start && @event.OriginalEventNumber.ToInt64() < start + count);
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
		return FilteredReadAllEventsBackwardAsync(position, maxCount, resolveLinkTos, null, 0, userCredentials);
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
