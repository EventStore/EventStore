using System;
using EventStore.Core.LogV3;
using EventStore.Core.Services;
using EventStore.LogV3;

namespace EventStore.Core.TransactionLog.LogRecords {
	// todo: when we have partition records etc there might be a baseclass to refactor
	// the string payload to.
	//
	//qq we probably dont want to treat these are normal events for projections, subscriptions, etc.
	// however, we do want to index them (for now) so that we can use them to look up the names.
	// we might need to suppress the StorageMessage.EventCommitted sending.
	public class LogV3StreamRecord : LogV3Record<StringPayloadRecord<Raw.StreamHeader>>, IPrepareLogRecord<long> {
		public long EventStreamId => LogV3SystemStreams.StreamsCreatedStreamNumber;
		// so we can see the stream name in the webui if we want
		public PrepareFlags Flags => PrepareFlags.SingleWrite | PrepareFlags.IsCommitted | PrepareFlags.IsJson;
		public long TransactionPosition => LogPosition;
		public int TransactionOffset => 0;
		// since we are storing only even ids, divide by 2 so that they are contiguous
		// then we can do a stream read of $streams-created if we want
		public long ExpectedVersion => Record.SubHeader.ReferenceNumber / 2 - 1;
		public Guid EventId => Record.Header.RecordId;
		//qq hmm
		public Guid CorrelationId { get; } = Guid.NewGuid();
		public string EventType => SystemEventTypes.StreamCreated;
		// so we can see the stream name in the webui if we want
		public ReadOnlyMemory<byte> Data => Record.Payload;
		public ReadOnlyMemory<byte> Metadata => ReadOnlyMemory<byte>.Empty;

		public string StreamName => Record.StringPayload;

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

		//qq is this covered by tests
		public IPrepareLogRecord<long> CopyForRetry(long logPosition, long transactionPosition) {
			return new LogV3StreamRecord(
				streamId: Record.Header.RecordId,
				timeStamp: Record.Header.TimeStamp,
				logPosition: logPosition,
				streamNumber: Record.SubHeader.ReferenceNumber,
				streamName: Record.StringPayload);
		}
	}
}
