using System;
using EventStore.LogCommon;
using StreamId = System.UInt32;

namespace EventStore.Core.XUnit.Tests.LogV3 {
	internal class MockEventRecord : IEventRecord {
		public Guid EventId { get; }
		public string EventType { get; }
		public ReadOnlyMemory<byte> Data { get; }
		public ReadOnlyMemory<byte> Metadata { get; }
		public EventFlags EventFlags { get; }
		public long? EventLogPosition { get; }
		public int? EventOffset { get; }
		public MockEventRecord(Guid eventId, string eventType, ReadOnlyMemory<byte> data, ReadOnlyMemory<byte> metadata, EventFlags eventFlags, long? eventLogPosition, int? eventOffset) {
			EventId = eventId;
			EventType = eventType;
			Data = data;
			Metadata = metadata;
			EventFlags = eventFlags;
			EventLogPosition = eventLogPosition;
			EventOffset = eventOffset;
		}
	}
}
