using System;
using EventStore.Common.Utils;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.LogCommon;

namespace EventStore.Core.Data {
	public class Event : IEventRecord {
		public long? LogPosition => null;
		public int? EventOffset => null;
		public Guid EventId { get; }
		public string EventType { get; }
		public ReadOnlyMemory<byte> Data { get; }
		public ReadOnlyMemory<byte> Metadata { get; }
		public EventFlags EventFlags => IsJson ? EventFlags.IsJson : EventFlags.None;
		public readonly bool IsJson;

		public Event(Guid eventId, string eventType, bool isJson, string data, string metadata)
			: this(
				eventId, eventType, isJson, Helper.UTF8NoBom.GetBytes(data),
				metadata != null ? Helper.UTF8NoBom.GetBytes(metadata) : null) {
		}

		public static int SizeOnDisk(string eventType, ReadOnlyMemory<byte> data, ReadOnlyMemory<byte> metadata) =>
			data.Length + metadata.Length + eventType.Length * 2;

		private static bool ExceedsMaximumSizeOnDisk(string eventType, ReadOnlyMemory<byte> data, ReadOnlyMemory<byte> metadata) =>
			SizeOnDisk(eventType, data, metadata) > TFConsts.EffectiveMaxLogRecordSize;

		public Event(Guid eventId, string eventType, bool isJson, ReadOnlyMemory<byte> data, ReadOnlyMemory<byte> metadata,
			bool allowEmptyEventType = false) {
			if (eventId == Guid.Empty)
				throw new ArgumentException("Empty eventId provided.", nameof(eventId));
			if (!allowEmptyEventType && string.IsNullOrEmpty(eventType))
				throw new ArgumentException("Empty eventType provided.", nameof(eventType));
			eventType ??= string.Empty;
			if (ExceedsMaximumSizeOnDisk(eventType, data, metadata))
				throw new ArgumentException("Record is too big.", nameof(data));

			EventId = eventId;
			EventType = eventType;
			IsJson = isJson;
			Data = data;
			Metadata = metadata;
		}
	}
}
