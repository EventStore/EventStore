using System;

namespace EventStore.Grpc {
	public class EventData {
		public readonly byte[] Data;
		public readonly Uuid EventId;
		public readonly byte[] Metadata;
		public readonly string Type;
		public readonly bool IsJson;

		public EventData(Uuid eventId, string type, byte[] data, byte[] metadata = default, bool isJson = true) {
			if (type == null) {
				throw new ArgumentNullException(nameof(type));
			}
			if (eventId == Uuid.Empty) {
				throw new ArgumentException(nameof(eventId));
			}

			EventId = eventId;
			Type = type;
			Data = data ?? Array.Empty<byte>();
			Metadata = metadata ?? Array.Empty<byte>();
			IsJson = isJson;
		}
	}
}
