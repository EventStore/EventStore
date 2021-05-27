using System;
using EventStore.Core.LogV3;
using EventStore.Core.Services;
using EventStore.LogV3;

namespace EventStore.Core.TransactionLog.LogRecords {
	// todo: when we have partition records etc there might be a baseclass to refactor
	// the string payload to.
	public class LogV3StreamRecord : LogV3Record<StringPayloadRecord<Raw.StreamHeader>>, IEquatable<LogV3StreamRecord>, IPrepareLogRecord<long> {
		public long EventStreamId => LogV3SystemStreams.StreamsCreatedStreamNumber;
		// so we can see the stream name in the webui if we want
		public PrepareFlags Flags => PrepareFlags.SingleWrite | PrepareFlags.IsCommitted | PrepareFlags.IsJson;
		public long TransactionPosition => LogPosition;
		public int TransactionOffset => 0;
		public long ExpectedVersion => StreamIdConverter.ToEventNumber(Record.SubHeader.ReferenceNumber) - 1;
		public Guid EventId => Record.Header.RecordId;
		public Guid CorrelationId { get; } = Guid.NewGuid();
		public string EventType => SystemEventTypes.StreamCreated;
		// so we can see the stream name in the webui if we want
		public ReadOnlyMemory<byte> Data => Record.Payload;
		public ReadOnlyMemory<byte> Metadata => ReadOnlyMemory<byte>.Empty;

		public string StreamName => Record.StringPayload;
		public long StreamNumber => Record.SubHeader.ReferenceNumber;

		public LogV3StreamRecord(
			Guid streamId,
			long logPosition,
			DateTime timeStamp,
			uint streamNumber,
			string streamName) : base() {

			Record = RecordCreator.CreateStreamRecord(
				streamId: streamId,
				timeStamp: timeStamp,
				logPosition: logPosition,
				streamNumber: streamNumber,
				streamName: streamName,
				partitionId: Guid.Empty,
				streamTypeId: Guid.Empty);
		}

		public LogV3StreamRecord(ReadOnlyMemory<byte> bytes) : base() {
			Record = StringPayloadRecord.Create(new RecordView<Raw.StreamHeader>(bytes));
		}

		public IPrepareLogRecord<long> CopyForRetry(long logPosition, long transactionPosition) {
			return new LogV3StreamRecord(
				streamId: Record.Header.RecordId,
				timeStamp: Record.Header.TimeStamp,
				logPosition: logPosition,
				streamNumber: Record.SubHeader.ReferenceNumber,
				streamName: Record.StringPayload);
		}

		public bool Equals(LogV3StreamRecord other) {
			if (other is null)
				return false;
			if (ReferenceEquals(this, other))
				return true;
			return
				other.StreamName == StreamName &&
				other.StreamNumber == StreamNumber &&
				other.Record.Bytes.Span.SequenceEqual(Record.Bytes.Span);
		}
	}
}
