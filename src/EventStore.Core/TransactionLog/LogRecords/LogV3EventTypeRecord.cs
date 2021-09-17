using System;
using EventStore.Core.LogV3;
using EventStore.LogV3;

namespace EventStore.Core.TransactionLog.LogRecords {
	public class LogV3EventTypeRecord : LogV3Record<StringPayloadRecord<Raw.EventTypeHeader>>, IPrepareLogRecord<uint> {
		public uint EventStreamId => LogV3SystemStreams.EventTypesCreatedStreamNumber;
		public PrepareFlags Flags => PrepareFlags.SingleWrite | PrepareFlags.IsCommitted | PrepareFlags.IsJson;
		public long TransactionPosition => LogPosition;
		public int TransactionOffset => 0;
		public long ExpectedVersion => EventTypeIdConverter.ToEventNumber(Record.SubHeader.ReferenceNumber) - 1;
		public Guid EventId => Record.Header.RecordId;
		public Guid CorrelationId { get; } = Guid.NewGuid();
		public uint EventType => Record.SubHeader.ReferenceNumber;
		// so we can see the event type in the webui if we want
		public ReadOnlyMemory<byte> Data => Record.Payload;
		public ReadOnlyMemory<byte> Metadata => ReadOnlyMemory<byte>.Empty;

		public string EventTypeName => Record.StringPayload;
		public uint EventTypeNumber => Record.SubHeader.ReferenceNumber;

		public LogV3EventTypeRecord(
			Guid eventTypeId,
			Guid parentEventTypeId,
			long logPosition,
			DateTime timeStamp,
			string eventType,
			uint eventTypeNumber,
			byte version,
			Guid partitionId) : base() {

			Record = RecordCreator.CreateEventTypeRecord(
				eventTypeId: eventTypeId,
				parentEventTypeId: parentEventTypeId,
				timeStamp: timeStamp,
				logPosition: logPosition,
				name: eventType,
				eventTypeNumber: eventTypeNumber,
				version: version,
				partitionId: partitionId);
		}

		public LogV3EventTypeRecord(ReadOnlyMemory<byte> bytes) : base() {
			Record = StringPayloadRecord.Create(new RecordView<Raw.EventTypeHeader>(bytes));
		}

		public IPrepareLogRecord<uint> CopyForRetry(long logPosition, long transactionPosition) {
			return new LogV3EventTypeRecord(
				eventTypeId: Record.Header.RecordId,
				timeStamp: Record.Header.TimeStamp,
				logPosition: logPosition,
				eventTypeNumber: Record.SubHeader.ReferenceNumber,
				eventType: Record.StringPayload,
				parentEventTypeId: Record.SubHeader.ParentEventTypeId,
				version: Record.Header.Version,
				partitionId: Record.SubHeader.PartitionId);
		}
	}
}
