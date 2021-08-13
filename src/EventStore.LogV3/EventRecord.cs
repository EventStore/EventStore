using System;
using System.Buffers;
using System.Runtime.InteropServices;
using EventStore.LogCommon;

namespace EventStore.LogV3 {
	// View of an event in a stream write record
	public struct EventRecord : IEventRecord {
		private readonly ReadOnlyMemory<byte> _headerMemory;
		private readonly ReadOnlyMemory<byte> _data;
		private readonly ReadOnlyMemory<byte> _metadata;

		public ref readonly Raw.EventHeader Header => ref MemoryMarshal.AsRef<Raw.EventHeader>(_headerMemory.Span);
		public ReadOnlyMemory<byte> Data => _data;
		public ReadOnlyMemory<byte> Metadata => _metadata;
		public EventSystemMetadata SystemMetadata { get; }
		public Guid EventId => SystemMetadata.EventId;
		public string EventType => SystemMetadata.EventType;
		public EventFlags EventFlags => (EventFlags) Header.Flags;
		public long? EventLogPosition { get; }
		public int? EventOffset { get; }

		// bytes already populated with a event record to read
		public EventRecord(ReadOnlyMemory<byte> bytes, long eventLogPosition, int eventOffset) {
			var slicer = bytes.Slicer();
			_headerMemory = slicer.Slice(Raw.EventHeader.Size);

			ref readonly var header = ref MemoryMarshal.AsRef<Raw.EventHeader>(_headerMemory.Span);

			var systemMetadata = slicer.Slice(header.SystemMetadataSize);
			_data = slicer.Slice(header.DataSize);
			_metadata = slicer.Remaining;

			SystemMetadata = EventSystemMetadata.Parser.ParseFrom(new ReadOnlySequence<byte>(systemMetadata));
			EventLogPosition = eventLogPosition;
			EventOffset = eventOffset;
		}
	}
}
