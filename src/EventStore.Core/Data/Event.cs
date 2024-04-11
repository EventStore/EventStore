using System;
using EventStore.Common.Utils;
using EventStore.Core.TransactionLog.Chunks;

namespace EventStore.Core.Data {
	public class Event {
		public readonly Guid EventId;
		public readonly string EventType;
		public readonly bool IsJson;
		public readonly byte[] Data;
		public readonly byte[] Metadata;
		public readonly byte[] SystemMetadata;

		public Event(Guid eventId, string eventType, bool isJson, string data, string metadata, string systemMetadata = null)
			: this(
				eventId, eventType, isJson, Helper.UTF8NoBom.GetBytes(data),
				metadata != null ? Helper.UTF8NoBom.GetBytes(metadata) : null,
				systemMetadata != null ? Helper.UTF8NoBom.GetBytes(systemMetadata) : null) {
		}

		public Event(Guid eventId, string eventType, bool isJson, byte[] data, byte[] metadata, byte[] systemMetadata = null) {
			if (eventId == Guid.Empty)
				throw new ArgumentException("Empty eventId provided.", nameof(eventId));
			if (string.IsNullOrEmpty(eventType))
				throw new ArgumentException("Empty eventType provided.", nameof(eventType));
			if (ExceedsMaximumSizeOnDisk(eventType, data, metadata, systemMetadata))
				throw new ArgumentException("Record is too big.", nameof(data));

			EventId = eventId;
			EventType = eventType;
			IsJson = isJson;
			Data = data ?? Array.Empty<byte>();
			Metadata = metadata ?? Array.Empty<byte>();
			SystemMetadata = systemMetadata ?? Array.Empty<byte>();
		}

		public static int SizeOnDisk(string eventType, byte[] data, byte[] metadata, byte[] systemMetadata) =>
			data?.Length ?? 0 + metadata?.Length ?? + systemMetadata?.Length ?? 0 + eventType.Length * 2;

		private static bool ExceedsMaximumSizeOnDisk(string eventType, byte[] data, byte[] metadata, byte[] systemMetadata) =>
			SizeOnDisk(eventType, data, metadata, systemMetadata) > TFConsts.EffectiveMaxLogRecordSize;
	}
}
