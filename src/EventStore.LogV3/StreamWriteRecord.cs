using System;
using System.Buffers;
using System.Runtime.InteropServices;
using EventStore.LogCommon;

namespace EventStore.LogV3 {
	// View of a stream write record
	// interprets the payload
	public struct StreamWriteRecord : IRecordView {
		private readonly ReadOnlyMemory<byte> _events;

		public ReadOnlyMemory<byte> Bytes => Record.Bytes;
		public RecordView<Raw.StreamWriteHeader> Record { get; }
		public ref readonly Raw.RecordHeader Header => ref Record.Header;
		public ref readonly Raw.StreamWriteId WriteId => ref Record.RecordId<Raw.StreamWriteId>();
		public ref readonly Raw.StreamWriteHeader SubHeader => ref Record.SubHeader;
		public ReadOnlyMemory<byte> Payload => Record.Payload;
		public StreamWriteSystemMetadata SystemMetadata { get; }
		public IEventRecord[] Events { get; }

		public StreamWriteRecord(RecordView<Raw.StreamWriteHeader> record) {
			Record = record;

			var slicer = Record.Payload.Slicer();
			var systemMetadata = slicer.Slice(Record.SubHeader.MetadataSize);
			_events = slicer.Remaining;

			SystemMetadata = StreamWriteSystemMetadata.Parser.ParseFrom(new ReadOnlySequence<byte>(systemMetadata));
			Events = GenerateEventRecords(Record, _events);
		}

		private static IEventRecord[] GenerateEventRecords(RecordView<Raw.StreamWriteHeader> record, ReadOnlyMemory<byte> events) {
			var slicer = events.Slicer();
			var eventRecords = new IEventRecord[record.SubHeader.Count];
			var firstEventOffset = sizeof(int) /* log record length (prefix) */
			                       + Raw.RecordHeader.Size /* log record header */
			                       + Raw.StreamWriteHeader.Size /* stream write sub header */
			                       + record.SubHeader.MetadataSize; /* metadata size */

			if (eventRecords.Length == 1) {
				ref readonly var eventHeader = ref MemoryMarshal.AsRef<Raw.EventHeader>(slicer.Remaining.Span);
				var eventBytes = slicer.Slice(eventHeader.EventSize);
				eventRecords[0] = new EventRecord(eventBytes, record.Header.LogPosition, firstEventOffset);
				return eventRecords;
			}

			for (var i = 0; i < eventRecords.Length; i++) {
				var backwardOffset = MemoryMarshal.AsRef<int>(slicer.Slice(sizeof(int)).Span);
				var eventOffset = firstEventOffset + slicer.Offset;
				var forwardOffset = MemoryMarshal.AsRef<int>(slicer.Slice(sizeof(int)).Span);
				if (backwardOffset >= 0) {
					throw new Exception($"backward offset should be negative but was: {backwardOffset}");
				}
				if (forwardOffset >= 0) {
					throw new Exception($"forward offset should be negative but was: {forwardOffset}");
				}
				var logRecordLength = record.Bytes.Length + 2 * sizeof(int);
				if (- (forwardOffset + backwardOffset) != logRecordLength) {
					throw new Exception($"sum of offsets does not match log record length: "
					                    +$" backward offset: {-backwardOffset}, forward offset: {-forwardOffset}, log record length: {logRecordLength}");
				}
				var logPosition = record.Header.LogPosition + -forwardOffset;

				ref readonly var eventHeader = ref MemoryMarshal.AsRef<Raw.EventHeader>(slicer.Remaining.Span);
				var eventBytes = slicer.Slice(eventHeader.EventSize);

				eventRecords[i] = new EventRecord(eventBytes, logPosition, eventOffset);
			}
			return eventRecords;
		}
	}
}
