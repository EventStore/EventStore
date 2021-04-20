using System;
using System.Collections.Generic;
using System.Text;
using EventStore.LogCommon;
using EventStore.LogV3;

namespace EventStore.Core.TransactionLog.LogRecords {
	//qq consider name... not sure we want this at all.... just cast and call
	public interface IPrepareList<T> {
		public IReadOnlyList<IPrepareLogRecord<T>> Prepares { get; }
	}

	//qqqq this is towards multiple events per write
	// implements iprepare because currently the strem write contains exactly one event
	// but when we generalise it to contain muliple events i exect we will be able to remove
	// implementing iprepare here.
	public class LogV3StreamWriteRecord : LogV3Record<StreamWriteRecord>,
		//qqqq temporary until we are writing in batch
		IPrepareLogRecord<long>,
		IPrepareList<long> {
		public LogV3StreamWriteRecord(ReadOnlyMemory<byte> bytes) : base() {
			Record = new StreamWriteRecord(new RecordView<Raw.StreamWriteHeader>(bytes));
			Prepares = GenPrepares(Record);
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

			//qqq might want some 'Ensures' in here. and other checks 

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

			Prepares = GenPrepares(Record);
		}

		private static IReadOnlyList<LogV3PrepareLogRecord> GenPrepares(StreamWriteRecord streamWrite) {
			var events = streamWrite.Events;
			var prepares = new LogV3PrepareLogRecord[events.Length];
			for (int i = 0; i < events.Length; i++)
				prepares[i] = new LogV3PrepareLogRecord(streamWrite, events[i]);
			return prepares;
		}

		// subrecordoffset is relative to the beginning of the record data
		public IPrepareLogRecord<long> GetPrepare(int eventOffset, out int length) {
			var eventRecord = Record.EventAtOffset(eventOffset);
			length = eventRecord.Header.EventSize;
			//qq dont want to construct a LogV3PrepareLogRecord here, they are all already in Prepares
			var prepare = new LogV3PrepareLogRecord(Record, eventRecord);
			return prepare;
		}


		public override LogRecordType RecordType => LogRecordType.Prepare;

		//qq these return the details of the first event for the sec, but soon we wont
		// need them at all.
		public PrepareFlags Flags => Record[0].Header.Flags;
		public long TransactionPosition => Record.Header.LogPosition;
		//qq i think when we put multiple events in one writerecord we might be able to populate this
		public int TransactionOffset => 0;
		public long ExpectedVersion => Record.SubHeader.StartingEventNumber - 1;
		public long EventStreamId => Record.SubHeader.StreamNumber;
		public Guid EventId => Record[0].Header.EventId;
		public Guid CorrelationId => Record.Header.RecordId;
		// temporarily storing the event type as the system metadata. later it will have a number.
		public string EventType => Encoding.UTF8.GetString(Record[0].SystemMetadata.Span);
		public ReadOnlyMemory<byte> Data => Record[0].Data;
		public ReadOnlyMemory<byte> Metadata => Record[0].Metadata;

		//qqqqq this is the whole record size but we probably just want it to be the prepare size... if we need this at all?
		public long InMemorySize => Record.Bytes.Length;

		//qq should be able to get rid of much of hte prvious prepare implemention in this file once we have multiple events in one write properly
		//qq hold on to the list in case this is caleld again (which looks like it is)
		public IReadOnlyList<LogV3PrepareLogRecord> Prepares { get; }
		IReadOnlyList<IPrepareLogRecord<long>> IPrepareList<long>.Prepares => Prepares;

		//qq is this covered by tests
		public IPrepareLogRecord<long> CopyForRetry(long logPosition, long transactionPosition) {
			return new LogV3StreamWriteRecord(
				logPosition: logPosition,
				correlationId: CorrelationId,
				eventId: EventId,
				eventStreamId: EventStreamId,
				expectedVersion: ExpectedVersion,
				timeStamp: TimeStamp,
				flags: Flags,
				eventType: EventType,
				data: Data.Span,
				metadata: Metadata.Span);
		}
	}
}
