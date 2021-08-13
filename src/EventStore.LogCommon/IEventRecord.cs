using System;

namespace EventStore.LogCommon {
	[Flags]
	public enum EventFlags : ushort {
		None = 0x00,
		IsJson = 0x01
	}

	public interface IEventRecord {
		long? EventLogPosition { get; }
		int? EventOffset { get; }
		Guid EventId { get; }
		string EventType { get; }
		ReadOnlyMemory<byte> Data { get; }
		ReadOnlyMemory<byte> Metadata { get; }
		EventFlags EventFlags { get; }
	}
}
