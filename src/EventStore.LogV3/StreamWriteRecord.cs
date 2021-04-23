using System;
using System.Runtime.InteropServices;

namespace EventStore.LogV3 {
	// View of a stream write record
	// interprets the payload
	public struct StreamWriteRecord : IRecordView {
		private readonly ReadOnlyMemory<byte> _event;
		public ReadOnlyMemory<byte> Bytes => Record.Bytes;
		public RecordView<Raw.StreamWriteHeader> Record { get; }
		public ref readonly Raw.RecordHeader Header => ref Record.Header;
		public ref readonly Raw.StreamWriteHeader SubHeader => ref Record.SubHeader;
		public ReadOnlyMemory<byte> SystemMetadata { get; }

		public StreamWriteRecord(RecordView<Raw.StreamWriteHeader> record) {
			Record = record;

			var slicer = Record.Payload.Slicer();
			SystemMetadata = slicer.Slice(Record.SubHeader.MetadataSize);
			_event = slicer.Remaining;
		}

		public EventRecord Event {
			get {
				var slicer = _event.Slicer();
				ref readonly var eventHeader = ref MemoryMarshal.AsRef<Raw.EventHeader>(slicer.Remaining.Span);
				var eventBytes = slicer.Slice(eventHeader.EventSize);
				return new EventRecord(eventBytes);
			}
		}
	}
}
