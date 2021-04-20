using System;
using System.IO;
using EventStore.Common.Utils;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.LogCommon;
using EventStore.LogV3;

namespace EventStore.Core.TransactionLog.LogRecords {
	//qq this idea here is logv3 can present these to the machinary for reading
	//qqqq maybe we need the concept of a read only repare (As in, a prepare that cannot be written out
	public class LogV3PrepareLogRecord : IPrepareLogRecord<long> {
		public const byte PrepareRecordVersion = LogRecordVersion.LogRecordV1;

		private readonly StreamWriteRecord _streamWrite;
		private readonly EventRecord _event;
		private readonly long _logPosition;

		public PrepareFlags Flags => (PrepareFlags)_event.Header.Flags;
		//qqqqqqqqqqqq these two... i think the transiaction position is probaly the position of the write record
		// and the transaction offset is probably the index of this event in the write record
		public long TransactionPosition => _streamWrite.Record.Header.LogPosition;
		public int TransactionOffset => 0; //qq i think when we put multiple events in one writerecord we might be able to populate this
		public long ExpectedVersion => _streamWrite.Record.SubHeader.StartingEventNumber - 1;

		public long EventStreamId => _streamWrite.Record.SubHeader.StreamNumber;

		public Guid EventId => _event.Header.EventId;
		public Guid CorrelationId => _streamWrite.Record.Header.RecordId; //qq make sure this and any other place using recordid as correlationid gets upated when we move the correlationid to metadata
		public string EventType => _event.Header.EventTypeNumber.ToString(); //qqqq
		public ReadOnlyMemory<byte> Data => _event.Data;
		public ReadOnlyMemory<byte> Metadata => _event.Metadata;

		public long InMemorySize => _event.Header.EventSize;

		public DateTime TimeStamp => _streamWrite.Record.Header.TimeStamp;

		public LogRecordType RecordType => LogRecordType.Prepare;

		//qq check, but i think this wants to be v1 because we are using the larger datatype that changed between v0 and v1
		public byte Version => PrepareRecordVersion;

		public long LogPosition => _logPosition;

		//public LogV3PrepareLogRecord(
		//	long logPosition,
		//	Guid correlationId,
		//	Guid eventId, //qq currently unused, is that bad? yes probably, this came from the client, it might be sad if we ignore it
		//	long transactionPosition,
		//	int transactionOffset,
		//	long eventStreamId,
		//	long expectedVersion,
		//	DateTime timeStamp,
		//	PrepareFlags flags,
		//	string eventType,
		//	ReadOnlyMemory<byte> data,
		//	ReadOnlyMemory<byte> metadata,
		//	byte prepareRecordVersion = PrepareRecordVersion) { //qq unused
		//	Ensure.NotEmptyGuid(correlationId, "correlationId");
		//	Ensure.NotEmptyGuid(eventId, "eventId");
		//	Ensure.Nonnegative(transactionPosition, "transactionPosition");
		//	if (transactionOffset < -1)
		//		throw new ArgumentOutOfRangeException("transactionOffset");
		//	Ensure.Positive(eventStreamId, "eventStreamId");
		//	if (expectedVersion < Core.Data.ExpectedVersion.Any)
		//		throw new ArgumentOutOfRangeException("expectedVersion");
		//	if (flags.HasNoneOf(PrepareFlags.IsCommitted))
		//		throw new ArgumentOutOfRangeException(
		//			nameof(flags),
		//			"v3 doesn't support non-committed prepares"); //qq wording

		//	//qqqqq what are we actually going to do here
		//	// we need somewhere to actually store the data, if we allocate (or rent)
		//	// a byte array for it then that will make it super easy to write to disk later
		//	//qqq later we should avoid makng a copy of the data/metadata. either have this
		//	// record allocated directly in the chunk and copy them in, or if this is getting allocated
		//	// elsewhere then dont copy the data/metadata just hold a reference to the readonlymemory

		//	// we either need a complete header before we set the data boundaries
		//	// or we need to update the databoundaries when the header changes
		//	// or we need to not store the data boundaries at all and calculate them on the fly
		//	Record = V3FactoryHelpers.CreateWriteRecord(
		//		logRecordType: (byte)LogRecordType.LogV3StreamWrite, //qqqq perhaps dont cast, perhaps translate?
		//		timeStamp: timeStamp,
		//		recordId: correlationId, //qqq huuuummmmmmmm
		//		correlationId: correlationId,
		//		logPosition: logPosition,

		//		parentPartition: 0, //qq
		//		partition: 0, //qq
		//		category: 0, //qq
		//		streamNumber: eventStreamId,
		//		startingEventNumber: expectedVersion + 1,

		//		recordMetadata: ReadOnlyMemory<byte>.Empty,
		//		eventData: data,
		//		eventMetadata: metadata,
		//		eventFlags: (short)flags); //qq probably need to translate flags

		//	_streamWrite = new StreamWriteRecord(Record);
		//	if (InMemorySize > TFConsts.MaxLogRecordSize)
		//		throw new Exception("Record too large.");
		//}

		//internal LogV3PrepareLogRecord(ReadOnlyMemory<byte> populatedBytes) : base(populatedBytes) {
		//	_streamWrite = new StreamWriteRecord(Record);

		//	//qqqq adjust this actual logic
		//	if (Version != LogRecordVersion.LogRecordV0 && Version != LogRecordVersion.LogRecordV1)
		//		throw new ArgumentException(string.Format(
		//			"PrepareRecord version {0} is incorrect. Supported version: {1}.", Version, PrepareRecordVersion));

		//	if (InMemorySize > TFConsts.MaxLogRecordSize)
		//		throw new Exception("Record too large.");
		//}

		public LogV3PrepareLogRecord(StreamWriteRecord streamWriteRecord, EventRecord eventRecord) {
			_streamWrite = streamWriteRecord;
			_event = eventRecord;
			// note that we do want to account for that here in the ACL rather than in the log records because
			// the length framing is implemented in the v2 machinery
			_logPosition =
				_streamWrite.Record.Header.LogPosition +
				//qq sizeof(int) is annoying, but accounts for the fact that the logposition of the record is
				// 4 bytes before the start of the data
				sizeof(int) +
				-eventRecord.Header.NegativeOffset +
				// the prepare position is offset an additional 4 bytes so that it can contain both the 
				// offsets (one for reading forward and one for reading backward)
				sizeof(int);
		}

		public override string ToString() {
			return string.Format("LogPosition: {0}, "
								 + "Flags: {1}, "
								 + "TransactionPosition: {2}, "
								 + "TransactionOffset: {3}, "
								 + "ExpectedVersion: {4}, "
								 + "StreamNumber: {5}, "
								 + "EventId: {6}, "
								 + "CorrelationId: {7}, "
								 + "TimeStamp: {8}, "
								 + "EventType: {9}, "
								 + "InMemorySize: {10}",
				LogPosition,
				Flags,
				TransactionPosition,
				TransactionOffset,
				ExpectedVersion,
				EventStreamId,
				EventId,
				CorrelationId,
				TimeStamp,
				EventType,
				InMemorySize);
		}

		//qqq dont want to implement this, ideally we can change split the interface into a hierarchy with a writable version
		// so we dont have to have this dummy implementation here.
		public void WriteTo(BinaryWriter writer) => throw new NotSupportedException();
		public int GetSizeWithLengthPrefixAndSuffix() => throw new NotSupportedException();

		//qq probably we dont want to support this, but if we do we would probably want it to advance to the next
		// sub record if it exists, or to the next main record from the last sub record (which is an extra 4 bytes further)
		//qqqqq actually i think we do need this for $all read forward. it needs to take an event address from the client
		// and then be able to advance forward
		public long GetNextLogPosition(long logicalPosition, int length) =>
			logicalPosition + length + sizeof(int); //qq this assumes we are the last event in the write

		//qqqqqqqq ^ aaaarg is this ever called passing in a different logical position to the record that we are getting the next/prev for??
		// if its just any logcal position then we can't easily tell what the result should be

		//qq similarly here we need to advance backwards...
		// this takes the postposition and length of this record, and returns the postposition of the 
		// previous record. BUT this isn't very good for us because we don't have framing in subrecords.
		// we dont want to add framing to the sub records really, it would take up space and it woudln't be necessayry because
		// the main record should contain enough context, and we do have to read that stuff anyyway to get the header
		public long GetPrevLogPosition(long logicalPosition, int length) => throw new NotSupportedException();

		public IPrepareLogRecord<long> CopyForRetry(long logPosition, long transactionPosition) {
			throw new NotImplementedException();
		}
	}
}
