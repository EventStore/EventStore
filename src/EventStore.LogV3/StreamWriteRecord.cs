using System;
using System.Runtime.InteropServices;

namespace EventStore.LogV3 {
	// View of a stream write record
	// interprets the payload
	public struct StreamWriteRecord : IRecordView {
		private readonly ReadOnlyMemory<byte> _events;
		public ReadOnlyMemory<byte> Bytes => Record.Bytes;
		public RecordView<Raw.StreamWriteHeader> Record { get; }
		public ref readonly Raw.RecordHeader Header => ref Record.Header;
		public ref readonly Raw.StreamWriteHeader SubHeader => ref Record.SubHeader;
		public ReadOnlyMemory<byte> Payload => Record.Payload;
		public ReadOnlyMemory<byte> SystemMetadata { get; }

		public StreamWriteRecord(RecordView<Raw.StreamWriteHeader> record) {
			Record = record;

			var slicer = Record.Payload.Slicer();
			SystemMetadata = slicer.Slice(Record.SubHeader.MetadataSize);
			_events = slicer.Remaining;
		}

		public EventRecord this[int i] {
			get {
				var offset = OffsetOfEvent(i);
				return EventAtOffset(offset);
			}
		}

		//qq want more efficient impleemntation
		// also consider holding on to the array, or otherwise making this a method
		// i wonder if we need this at all, or if we just need an iterator
		public EventRecord[] Events {
			get {
				var events = new EventRecord[Record.SubHeader.Count];
				for (int i = 0; i < events.Length; i++) {
					events[i] = this[i];
				}
				return events;
			}
		}

		/// offset relative to bytes of the record (NOT the log position, which includes the 4 size bytes)
		private int OffsetOfEvent(int i) {
			if (i >= Record.SubHeader.Count)
				throw new IndexOutOfRangeException($"Index: {i}. EventCount: {Record.SubHeader.Count}");

			var offset = Record.PayloadOffset + SystemMetadata.Length;
			var slicer = _events.Slicer();

			for (int j = 0; j < i; j++) {
				ref readonly var eventHeader = ref MemoryMarshal.AsRef<Raw.EventHeader>(slicer.Remaining.Span);
				offset += eventHeader.EventSize;
				slicer.Slice(eventHeader.EventSize);
			}

			return offset;
		}

		/// offset relative to bytes of the record (NOT the log position, which includes the 4 size bytes)
		//qq necessarily needs to be public?
		public EventRecord EventAtOffset(int offset) {
			var slicer = Record.Bytes.Slicer();
			slicer.Slice(offset);
			ref readonly var eventHeader = ref MemoryMarshal.AsRef<Raw.EventHeader>(slicer.Remaining.Span);
			var eventBytes = slicer.Slice(eventHeader.EventSize);
			return new EventRecord(eventBytes);
		}
	}
}
