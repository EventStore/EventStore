using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Data;
using ClientMessage = EventStore.ClientAPI.Messages.ClientMessage;

namespace EventStore.ClientAPI.Embedded {
	internal static class ClientMessageConverterExtensions {
		public static ClientMessage.ResolvedIndexedEvent[] ConvertToClientResolvedIndexEvents(
			this EventStore.Core.Data.ResolvedEvent[] events) {
			var resolvedEvents = new ClientMessage.ResolvedIndexedEvent[events.Length];

			for (int i = 0; i < events.Length; i++) {
				resolvedEvents[i] = events[i].ConvertToClientResolvedIndexEvent();
			}

			return resolvedEvents;
		}

		public static ClientMessage.ResolvedIndexedEvent ConvertToClientResolvedIndexEvent(
			this EventStore.Core.Data.ResolvedEvent @event) {
			return new ClientMessage.ResolvedIndexedEvent(@event.Event.ToClientMessageEventRecord(),
				@event.Link.ToClientMessageEventRecord());
		}

		public static ClientMessage.ResolvedEvent[] ConvertToClientResolvedEvents(
			this EventStore.Core.Data.ResolvedEvent[] events) {
			var resolvedEvents = new ClientMessage.ResolvedEvent[events.Length];

			for (int i = 0; i < events.Length; i++) {
				resolvedEvents[i] = events[i].ConvertToClientResolvedEvent();
			}

			return resolvedEvents;
		}

		public static ClientMessage.ResolvedEvent ConvertToClientResolvedEvent(
			this EventStore.Core.Data.ResolvedEvent @event) {
			return new ClientMessage.ResolvedEvent(@event.Event.ToClientMessageEventRecord(),
				@event.Link.ToClientMessageEventRecord(), @event.OriginalPosition.Value.CommitPosition,
				@event.OriginalPosition.Value.PreparePosition);
		}

		public static Event[] ConvertToEvents(this IEnumerable<EventData> events) {
			return events.Select(ConvertToEvent).ToArray();
		}

		public static Event ConvertToEvent(this EventData e) {
			return new Event(e.EventId, e.Type, e.IsJson, e.Data, e.Metadata);
		}

		private static ClientMessage.EventRecord ToClientMessageEventRecord(this EventRecord eventRecord) {
			if (eventRecord == null) return null;
			return new ClientMessage.EventRecord(
				eventRecord.EventStreamId, eventRecord.EventNumber,
				eventRecord.EventId.ToByteArray(), eventRecord.EventType, eventRecord.IsJson ? 1 : 0,
				eventRecord.IsJson ? 1 : 0,
				eventRecord.Data,
				eventRecord.Metadata,
				eventRecord.TimeStamp.ToBinary(),
				(long)(eventRecord.TimeStamp - new DateTime(1970, 1, 1)).TotalMilliseconds);
		}
	}
}
