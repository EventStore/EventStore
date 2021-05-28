using System;
using EventStore.Common.Utils;
using EventStore.Core.LogV3;
using EventStore.LogCommon;
using EventStore.LogV3;
using StreamId = System.UInt32;

namespace EventStore.Core.TransactionLog.LogRecords {
	// implements iprepare because currently the strem write contains exactly one event
	// but when we generalise it to contain muliple events i exect we will be able to remove
	// implementing iprepare here.
	public class LogV3StreamWriteRecord : LogV3Record<StreamWriteRecord>, IEquatable<LogV3StreamWriteRecord>, IPrepareLogRecord<StreamId> {
		public LogV3StreamWriteRecord(ReadOnlyMemory<byte> bytes) : base() {
			Record = new StreamWriteRecord(new RecordView<Raw.StreamWriteHeader>(bytes));
		}

		public LogV3StreamWriteRecord(
			long logPosition,
			long transactionPosition,
			int transactionOffset,
			Guid correlationId,
			Guid eventId,
			StreamId eventStreamId,
			long expectedVersion,
			DateTime timeStamp,
			PrepareFlags flags,
			string eventType,
			ReadOnlySpan<byte> data,
			ReadOnlySpan<byte> metadata) {

			Ensure.Nonnegative(logPosition, "logPosition");
			Ensure.NotEmptyGuid(correlationId, "correlationId");
			Ensure.NotEmptyGuid(eventId, "eventId");
			Ensure.Nonnegative(transactionPosition, "transactionPosition");
			if (transactionOffset < -1)
				throw new ArgumentOutOfRangeException("transactionOffset");
			if (eventStreamId < LogV3SystemStreams.FirstVirtualStream)
				throw new ArgumentOutOfRangeException("eventStreamId", eventStreamId, null);
			if (expectedVersion < Core.Data.ExpectedVersion.Any)
				throw new ArgumentOutOfRangeException("expectedVersion");
			eventType ??= "";

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
		public StreamId EventStreamId => Record.WriteId.StreamNumber;
		public Guid EventId => Record.Event.SystemMetadata.EventId;
		public Guid CorrelationId => Record.SystemMetadata.CorrelationId;
		// temporarily storing the event type as the system metadata. later it will have a number.
		public string EventType => Record.Event.SystemMetadata.EventType;
		public ReadOnlyMemory<byte> Data => Record.Event.Data;
		public ReadOnlyMemory<byte> Metadata => Record.Event.Metadata;

		public IPrepareLogRecord<StreamId> CopyForRetry(long logPosition, long transactionPosition) {
			return new LogV3StreamWriteRecord(
				logPosition: logPosition,
				transactionPosition: transactionPosition,
				transactionOffset: TransactionOffset,
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

		public bool Equals(LogV3StreamWriteRecord other) {
			if (ReferenceEquals(null, other)) return false;
			if (ReferenceEquals(this, other)) return true;
			return 	other.Version == Version
			        && other.LogPosition == LogPosition
			        && other.TimeStamp.Equals(TimeStamp)
			        && other.RecordType == RecordType
			        && other.Flags == Flags
			        && other.TransactionPosition == TransactionPosition
			        && other.TransactionOffset == TransactionOffset
			        && other.ExpectedVersion == ExpectedVersion
			        && other.EventStreamId.Equals(EventStreamId)
			        && other.EventId == EventId
			        && other.CorrelationId == CorrelationId
			        && other.EventType.Equals(EventType)
			        && other.Data.Span.SequenceEqual(Data.Span)
			        && other.Metadata.Span.SequenceEqual(Metadata.Span);
		}

		public override bool Equals(object obj) {
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((LogV3StreamWriteRecord) obj);
		}

		public override int GetHashCode() {
			unchecked {
				var result = Version.GetHashCode();
				result = (result * 397) ^ LogPosition.GetHashCode();
				result = (result * 397) ^ TimeStamp.GetHashCode();
				result = (result * 397) ^ RecordType.GetHashCode();
				result = (result * 397) ^ Flags.GetHashCode();
				result = (result * 397) ^ TransactionPosition.GetHashCode();
				result = (result * 397) ^ TransactionOffset;
				result = (result * 397) ^ ExpectedVersion.GetHashCode();
				result = (result * 397) ^ EventStreamId.GetHashCode();
				result = (result * 397) ^ EventId.GetHashCode();
				result = (result * 397) ^ CorrelationId.GetHashCode();
				result = (result * 397) ^ EventType.GetHashCode();
				result = (result * 397) ^ Data.GetHashCode();
				result = (result * 397) ^ Metadata.GetHashCode();
				return result;
			}
		}
	}
}
