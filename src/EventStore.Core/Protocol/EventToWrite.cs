using System;

namespace EventStore.Core.Protocol;

public class EventToWrite {
	public Guid EventId { get; init; }
	public string EventType { get; init; }
	public string ContentType { get; init; }
	public ReadOnlyMemory<byte> Data { get; init; }
	public ReadOnlyMemory<byte> Metadata { get; init; }

	public EventToWrite(
		Guid eventId,
		string eventType,
		string contentType,
		ReadOnlyMemory<byte> data,
		ReadOnlyMemory<byte> metadata) {

		EventId = eventId;
		EventType = eventType;
		ContentType = contentType;
		Data = data;
		Metadata = metadata;
	}
}
