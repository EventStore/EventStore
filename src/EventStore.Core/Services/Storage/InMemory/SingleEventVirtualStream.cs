// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.TransactionLog.LogRecords;
using static EventStore.Core.Messages.ClientMessage;

namespace EventStore.Core.Services.Storage.InMemory;

// threading: we expect to handle one Write at a time, but Reads can happen concurrently
// with the write and with other reads.
public class SingleEventVirtualStream(IPublisher publisher, InMemoryLog memLog, string streamName) : IVirtualStreamReader {
	private const PrepareFlags Flags = PrepareFlags.Data | PrepareFlags.IsCommitted | PrepareFlags.IsJson;
	private long _eventNumber = 0;
	private EventRecord _lastEvent;

	public ValueTask<ReadStreamEventsForwardCompleted> ReadForwards(ReadStreamEventsForward msg, CancellationToken token) {
		ReadStreamResult result;
		ResolvedEvent[] events;
		long nextEventNumber, lastEventNumber;

		var lastEvent = _lastEvent;
		if (lastEvent == null) {
			// no stream
			result = ReadStreamResult.NoStream;
			events = [];
			nextEventNumber = -1;
			lastEventNumber = ExpectedVersion.NoStream;
		} else {
			result = ReadStreamResult.Success;
			nextEventNumber = lastEvent.EventNumber + 1;
			lastEventNumber = lastEvent.EventNumber;

			if (msg.FromEventNumber > lastEvent.EventNumber) {
				// from too high. empty read
				events = [];
			} else {
				// read containing the event
				events = [ResolvedEvent.ForUnresolvedEvent(lastEvent)];
			}
		}

		return ValueTask.FromResult(new ReadStreamEventsForwardCompleted(
			msg.CorrelationId,
			msg.EventStreamId,
			msg.FromEventNumber,
			msg.MaxCount,
			result,
			events,
			StreamMetadata.Empty,
			isCachePublic: false,
			error: string.Empty,
			nextEventNumber: nextEventNumber,
			lastEventNumber: lastEventNumber,
			isEndOfStream: true,
			tfLastCommitPosition: memLog.GetLastCommitPosition()));
	}

	public ValueTask<ReadStreamEventsBackwardCompleted> ReadBackwards(ReadStreamEventsBackward msg, CancellationToken token) {
		ReadStreamResult result;
		ResolvedEvent[] events;
		long adjustedFromEventNumber, lastEventNumber;

		var lastEvent = _lastEvent;
		if (lastEvent == null) {
			// no stream
			adjustedFromEventNumber = msg.FromEventNumber;
			result = ReadStreamResult.NoStream;
			events = [];
			lastEventNumber = ExpectedVersion.NoStream;
		} else {
			result = ReadStreamResult.Success;
			lastEventNumber = lastEvent.EventNumber;

			var readFromEnd = msg.FromEventNumber < 0;
			adjustedFromEventNumber = readFromEnd ? lastEvent.EventNumber : msg.FromEventNumber;

			if (adjustedFromEventNumber < lastEvent.EventNumber) {
				// from too low. empty read
				events = [];
			} else {
				// read containing the event
				events = [ResolvedEvent.ForUnresolvedEvent(lastEvent)];
			}
		}

		return ValueTask.FromResult(new ReadStreamEventsBackwardCompleted(
			correlationId: msg.CorrelationId,
			eventStreamId: msg.EventStreamId,
			fromEventNumber: adjustedFromEventNumber,
			maxCount: msg.MaxCount,
			result: result,
			events: events,
			streamMetadata: StreamMetadata.Empty,
			isCachePublic: false,
			error: string.Empty,
			nextEventNumber: -1,
			lastEventNumber: lastEventNumber,
			isEndOfStream: true,
			tfLastCommitPosition: memLog.GetLastCommitPosition()));
	}

	public ValueTask<long> GetLastEventNumber(string streamId) {
		var lastNumber = _lastEvent?.EventNumber ?? -1;
		return ValueTask.FromResult(lastNumber);
	}

	public ValueTask<long> GetLastIndexedPosition() => ValueTask.FromResult(-1L);

	public bool OwnStream(string streamId) => streamId == streamName;

	public void Write(string eventType, ReadOnlyMemory<byte> data) {
		var commitPosition = memLog.GetNextCommitPosition();
		var prepare = new PrepareLogRecord(
			logPosition: commitPosition,
			correlationId: Guid.NewGuid(),
			eventId: Guid.NewGuid(),
			transactionPosition: commitPosition,
			transactionOffset: 0,
			eventStreamId: streamName,
			eventStreamIdSize: null,
			expectedVersion: _eventNumber - 1,
			timeStamp: DateTime.Now,
			flags: Flags,
			eventType: eventType,
			eventTypeSize: null,
			data: data,
			metadata: Array.Empty<byte>());
		_lastEvent = new EventRecord(_eventNumber, prepare, streamName, eventType);
		publisher.Publish(new StorageMessage.InMemoryEventCommitted(commitPosition, _lastEvent));
		_eventNumber++;
	}
}
