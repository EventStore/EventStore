using System;

namespace EventStore.Grpc {
	public class EventData {
		public readonly byte[] Data;
		public readonly Guid EventId;
		public readonly byte[] Metadata;
		public readonly string Type;
		public readonly bool IsJson;

		public EventData(Guid eventId, string type, byte[] data, byte[] metadata = default, bool isJson = true) {
			if (eventId == Guid.Empty) {
				throw new ArgumentOutOfRangeException(nameof(eventId));
			}
			if (type == null) {
				throw new ArgumentNullException(nameof(type));
			}

			EventId = eventId;
			Type = type;
			Data = data ?? Array.Empty<byte>();
			Metadata = metadata ?? Array.Empty<byte>();
			IsJson = isJson;
		}
	}
}
