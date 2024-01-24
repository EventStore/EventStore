using System;
using System.Text.Json;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Services;

// threading: we expect to handle one StateChangeMessage at a time, but Reads can happen concurrently
// with those handlings and with other reads.
public class NodeStateListenerService :
	IInMemoryStreamReader,
	IHandle<SystemMessage.IStateChangeMessage> {
	private readonly IPublisher _publisher;
	public const string EventType = "$NodeStateChanged";
	private const PrepareFlags Flags = PrepareFlags.Data | PrepareFlags.IsCommitted | PrepareFlags.IsJson;
	private long _eventNumber;
	private EventRecord _lastEvent;

	public NodeStateListenerService(IPublisher publisher) {
		_publisher = publisher;
		_eventNumber = 0;
	}

	public ClientMessage.ReadStreamEventsForwardCompleted ReadForwards(ClientMessage.ReadStreamEventsForward msg) {
		ReadStreamResult result;
		ResolvedEvent[] events;
		long nextEventNumber, lastEventNumber, tfLastCommitPosition;

		var lastEvent = _lastEvent;
		if (lastEvent == null) {
			// no stream
			result = ReadStreamResult.NoStream;
			events = Array.Empty<ResolvedEvent>();
			nextEventNumber = -1;
			lastEventNumber = ExpectedVersion.NoStream;
			tfLastCommitPosition = -1;
		} else {
			result = ReadStreamResult.Success;
			nextEventNumber = lastEvent.EventNumber + 1;
			lastEventNumber = lastEvent.EventNumber;
			tfLastCommitPosition = lastEvent.EventNumber;

			if (msg.FromEventNumber > lastEvent.EventNumber) {
				// from too high. empty read
				events = Array.Empty<ResolvedEvent>();
			} else {
				// read containing the event
				events = new[] { ResolvedEvent.ForUnresolvedEvent(lastEvent) };
			}
		}

		return new ClientMessage.ReadStreamEventsForwardCompleted(
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
			tfLastCommitPosition: tfLastCommitPosition);
	}

	public ClientMessage.ReadStreamEventsBackwardCompleted ReadBackwards(ClientMessage.ReadStreamEventsBackward msg) {
		ReadStreamResult result;
		ResolvedEvent[] events;
		long adjustedFromEventNumber, lastEventNumber, tfLastCommitPosition;

		var lastEvent = _lastEvent;
		if (lastEvent == null) {
			// no stream
			adjustedFromEventNumber = msg.FromEventNumber;
			result = ReadStreamResult.NoStream;
			events = Array.Empty<ResolvedEvent>();
			lastEventNumber = ExpectedVersion.NoStream;
			tfLastCommitPosition = -1L;
		} else {
			result = ReadStreamResult.Success;
			lastEventNumber = lastEvent.EventNumber;
			tfLastCommitPosition = lastEvent.EventNumber;

			var readFromEnd = msg.FromEventNumber < 0;
			adjustedFromEventNumber = readFromEnd ? lastEvent.EventNumber : msg.FromEventNumber;

			if (adjustedFromEventNumber < lastEvent.EventNumber) {
				// from too low. empty read
				events = Array.Empty<ResolvedEvent>();
			} else {
				// read containing the event
				events = new[] { ResolvedEvent.ForUnresolvedEvent(lastEvent) };
			}
		}

		return new ClientMessage.ReadStreamEventsBackwardCompleted(
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
			tfLastCommitPosition: tfLastCommitPosition);
	}

	public void Handle(SystemMessage.IStateChangeMessage message) {
		var payload = new { state = message.State.ToString() };
		var data = JsonSerializer.SerializeToUtf8Bytes(payload);
		var prepare = new PrepareLogRecord(
			logPosition: _eventNumber,
			correlationId: message.CorrelationId,
			eventId: Guid.NewGuid(),
			transactionPosition: _eventNumber,
			transactionOffset: 0,
			eventStreamId: SystemStreams.NodeStateStream,
			eventStreamIdSize: null,
			expectedVersion: _eventNumber - 1,
			timeStamp: DateTime.Now,
			flags: Flags,
			eventType: EventType,
			eventTypeSize: null,
			data: data,
			metadata: Array.Empty<byte>());
		_lastEvent = new EventRecord(_eventNumber, prepare, SystemStreams.NodeStateStream, EventType);
		_publisher.Publish(new StorageMessage.InMemoryEventCommitted(_eventNumber, _lastEvent));
		_eventNumber++;
	}
}
