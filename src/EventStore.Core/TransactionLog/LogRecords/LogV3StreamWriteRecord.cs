using System;
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

		public LogV3StreamWriteRecord(
			long logPosition,
			long transactionPosition,
			int transactionOffset,
			Guid correlationId,
			Guid eventId,
			long eventStreamId,
			long expectedVersion,
			DateTime timeStamp,
			PrepareFlags flags,
			string eventType,
			ReadOnlySpan<byte> data,
			ReadOnlySpan<byte> metadata) {

			Record = RecordCreator.CreateStreamWriteRecordForSingleEvent(
				timeStamp: timeStamp,
				correlationId: correlationId,
				logPosition: logPosition,
				transactionPosition: transactionPosition,
				transactionOffset: transactionOffset,
				streamNumber: eventStreamId,
				startingEventNumber: expectedVersion + 1,
				eventId: eventId,
				// temporarily storing the event type as the system metadata. later it will have a number.
				eventType: eventType,
				eventData: data,
				eventMetadata: metadata,
				// todo: translate
				eventFlags: (Raw.EventFlags)flags);
		}

		public override LogRecordType RecordType => LogRecordType.Prepare;

		// todo: translate
		public PrepareFlags Flags => (PrepareFlags)Record.Event.Header.Flags;
		public long TransactionPosition => Record.SystemMetadata.TransactionPosition;
		public int TransactionOffset => Record.SystemMetadata.TransactionOffset;
		public long ExpectedVersion => Record.WriteId.StartingEventNumber - 1;
		public long EventStreamId => Record.WriteId.StreamNumber;
		public Guid EventId => Record.Event.SystemMetadata.EventId;
		public Guid CorrelationId => Record.SystemMetadata.CorrelationId;
		// temporarily storing the event type as the system metadata. later it will have a number.
		public string EventType => Record.Event.SystemMetadata.EventType;
		public ReadOnlyMemory<byte> Data => Record.Event.Data;
		public ReadOnlyMemory<byte> Metadata => Record.Event.Metadata;
	}
}
