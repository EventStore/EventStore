using System;
using EventStore.Common.Utils;
using EventStore.Core.TransactionLog.Chunks;

namespace EventStore.Core.Data {
	public class Event {
		public readonly Guid EventId;
		public readonly string EventType;
		public readonly int? EventTypeSize;
		public readonly bool IsJson;
		public readonly byte[] Data;
		public readonly byte[] Metadata;

		public Event(Guid eventId, string eventType, bool isJson, string data, string metadata)
			: this(eventId, eventType, null, isJson,Helper.UTF8NoBom.GetBytes(data),
				metadata != null ? Helper.UTF8NoBom.GetBytes(metadata) : null) { }
		
		public Event(Guid eventId, string eventType, bool isJson, byte[] data, byte[] metadata)
			: this(eventId, eventType, null, isJson, data, metadata) { }
		
		public Event(Guid eventId, ReadOnlySpan<byte> eventType, bool isJson, byte[] data, byte[] metadata)
			: this(eventId, Helper.UTF8NoBom.GetString(eventType), eventType.Length, isJson, data, metadata) { }

		public static int SizeOnDisk(int eventTypeSize, byte[] data, byte[] metadata) =>
			data?.Length ?? 0 + metadata?.Length ?? 0 + eventTypeSize;

		private static bool ExceedsMaximumSizeOnDisk(int eventTypeSize, byte[] data, byte[] metadata) =>
			SizeOnDisk(eventTypeSize, data, metadata) > TFConsts.EffectiveMaxLogRecordSize;

		private Event(Guid eventId, string eventType, int? eventTypeSize, bool isJson, byte[] data, byte[] metadata) {
			if (eventId == Guid.Empty)
				throw new ArgumentException("Empty eventId provided.", nameof(eventId));
			if (string.IsNullOrEmpty(eventType))
				throw new ArgumentException("Empty eventType provided.", nameof(eventType));
			if (ExceedsMaximumSizeOnDisk(eventTypeSize ?? eventType.Length * 2, data, metadata))
				throw new ArgumentException("Record is too big.", nameof(data));

			EventId = eventId;
			EventType = eventType;
			EventTypeSize = eventTypeSize;
			IsJson = isJson;
			Data = data ?? Array.Empty<byte>();
			Metadata = metadata ?? Array.Empty<byte>();
		}
	}
}
