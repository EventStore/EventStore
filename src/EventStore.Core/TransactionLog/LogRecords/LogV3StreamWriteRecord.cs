using System;
using System.Text;
using EventStore.LogCommon;
using EventStore.LogV3;

namespace EventStore.Core.TransactionLog.LogRecords {
	// implements iprepare because currently the strem write contains exactly one event
	// but when we generalise it to contain muliple events i exect we will be able to remove
	// implementing iprepare here.
	public class LogV3StreamWriteRecord : LogV3Record<StreamWriteRecord>, IPrepareLogRecord<long> {
		public LogV3StreamWriteRecord(ReadOnlyMemory<byte> bytes) : base() {
			Record = new StreamWriteRecord(new RecordView<Raw.StreamWriteHeader>(bytes));
		}

		public unsafe LogV3StreamWriteRecord(
			long logPosition,
			Guid correlationId,
			Guid eventId,
			long eventStreamId,
			long expectedVersion,
			DateTime timeStamp,
			PrepareFlags flags,
			string eventType,
			ReadOnlySpan<byte> data,
			ReadOnlySpan<byte> metadata) {

			Span<byte> eventTypeBytes = stackalloc byte[RecordCreator.MeasureString(eventType)];
			RecordCreator.PopulateString(eventType, eventTypeBytes);

			Record = RecordCreator.CreateStreamWriteRecordForSingleEvent(
				timeStamp: timeStamp,
				recordId: correlationId,
				logPosition: logPosition,
				streamNumber: eventStreamId,
				startingEventNumber: expectedVersion + 1,
				recordMetadata: ReadOnlySpan<byte>.Empty,
				eventId: eventId,
				// temporarily storing the event type as the system metadata. later it will have a number.
				eventSystemMetadata: eventTypeBytes,
				eventData: data,
				eventMetadata: metadata,
				eventFlags: flags);
		}

		public override LogRecordType RecordType => LogRecordType.Prepare;

		public PrepareFlags Flags => Record.Event.Header.Flags;
		public long TransactionPosition => Record.Header.LogPosition;
		public int TransactionOffset => 0;
		public long ExpectedVersion => Record.SubHeader.StartingEventNumber - 1;
		public long EventStreamId => Record.SubHeader.StreamNumber;
		public Guid EventId => Record.Event.Header.EventId;
		public Guid CorrelationId => Record.Header.RecordId;
		// temporarily storing the event type as the system metadata. later it will have a number.
		public string EventType => Encoding.UTF8.GetString(Record.Event.SystemMetadata.Span);
		public ReadOnlyMemory<byte> Data => Record.Event.Data;
		public ReadOnlyMemory<byte> Metadata => Record.Event.Metadata;
	}
}
