using System;
using System.Runtime.InteropServices;

namespace EventStore.LogV3 {
	// View of an event in a stream write record
	public struct EventRecord {
		private readonly ReadOnlyMemory<byte> _headerMemory;
		private readonly ReadOnlyMemory<byte> _systemMetadata;
		private readonly ReadOnlyMemory<byte> _data;
		private readonly ReadOnlyMemory<byte> _metadata;

		public ref readonly Raw.EventHeader Header => ref MemoryMarshal.AsRef<Raw.EventHeader>(_headerMemory.Span);
		public ReadOnlyMemory<byte> SystemMetadata => _systemMetadata;
		public ReadOnlyMemory<byte> Data => _data;
		public ReadOnlyMemory<byte> Metadata => _metadata;

		// bytes already populated with a event record to read
		public EventRecord(ReadOnlyMemory<byte> bytes) {
			var slicer = bytes.Slicer();
			_headerMemory = slicer.Slice(Raw.EventHeader.Size);

			ref readonly var header = ref MemoryMarshal.AsRef<Raw.EventHeader>(_headerMemory.Span);

			_systemMetadata = slicer.Slice(header.SystemMetadataSize);
			_data = slicer.Slice(header.DataSize);
			_metadata = slicer.Remaining;
		}
	}

	//qq need this?
	public struct MutableEventRecordView {
		private readonly Memory<byte> _headerMemory;
		private readonly Memory<byte> _systemMetadata;
		private readonly Memory<byte> _data;
		private readonly Memory<byte> _metadata;

		public ref Raw.EventHeader Header => ref MemoryMarshal.AsRef<Raw.EventHeader>(_headerMemory.Span);
		public Memory<byte> SystemMetadata => _systemMetadata;
		public Memory<byte> Data => _data;
		public Memory<byte> Metadata => _metadata;

		// bytes already populated with a event record to read
		public MutableEventRecordView(Memory<byte> bytes) {
			var slicer = bytes.Slicer();
			_headerMemory = slicer.Slice(Raw.EventHeader.Size);

			ref readonly var header = ref MemoryMarshal.AsRef<Raw.EventHeader>(_headerMemory.Span);

			_systemMetadata = slicer.Slice(header.SystemMetadataSize);
			_data = slicer.Slice(header.DataSize);
			_metadata = slicer.Remaining;
		}
	}
}
