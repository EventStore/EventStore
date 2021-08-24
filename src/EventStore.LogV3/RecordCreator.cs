using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Unicode;
using EventStore.LogCommon;
using Google.Protobuf;

namespace EventStore.LogV3 {
	// Nice-to-use wrappers for creating and populating the raw structures.
	public static class RecordCreator {
		// these exist to make the code more obviously correct
		private static byte CurrentVersion(this ref Raw.EpochHeader _) => LogRecordVersion.LogRecordV1;
		private static byte CurrentVersion(this ref Raw.PartitionHeader _) => LogRecordVersion.LogRecordV0;
		private static byte CurrentVersion(this ref Raw.PartitionTypeHeader _) => LogRecordVersion.LogRecordV0;
		private static byte CurrentVersion(this ref Raw.StreamHeader _) => LogRecordVersion.LogRecordV0;
		private static byte CurrentVersion(this ref Raw.StreamTypeHeader _) => LogRecordVersion.LogRecordV0;
		private static byte CurrentVersion(this ref Raw.StreamWriteHeader _) => LogRecordVersion.LogRecordV0;
		private static byte CurrentVersion(this ref Raw.EventTypeHeader _) => LogRecordVersion.LogRecordV0;
		private static byte CurrentVersion(this ref Raw.ContentTypeHeader _) => LogRecordVersion.LogRecordV0;
		private static byte CurrentVersion(this ref Raw.TransactionStartHeader _) => LogRecordVersion.LogRecordV0;
		private static byte CurrentVersion(this ref Raw.TransactionEndHeader _) => LogRecordVersion.LogRecordV0;

		private static LogRecordType Type(this ref Raw.EpochHeader _) => LogRecordType.System;
		private static LogRecordType Type(this ref Raw.PartitionHeader _) => LogRecordType.Partition;
		private static LogRecordType Type(this ref Raw.PartitionTypeHeader _) => LogRecordType.PartitionType;
		private static LogRecordType Type(this ref Raw.StreamHeader _) => LogRecordType.Stream;
		private static LogRecordType Type(this ref Raw.StreamTypeHeader _) => LogRecordType.StreamType;
		private static LogRecordType Type(this ref Raw.StreamWriteHeader _) => LogRecordType.StreamWrite;
		private static LogRecordType Type(this ref Raw.EventTypeHeader _) => LogRecordType.EventType;
		private static LogRecordType Type(this ref Raw.ContentTypeHeader _) => LogRecordType.ContentType;
		private static LogRecordType Type(this ref Raw.TransactionStartHeader _) => LogRecordType.TransactionStart;
		private static LogRecordType Type(this ref Raw.TransactionEndHeader _) => LogRecordType.TransactionEnd;
		
		// todo: limit of 100 bytes when we know a good way to report the error to the client.
		// throwing here will crash the writer.
		const int MaxStringBytes = int.MaxValue;
		public static void PopulateString(string str, Span<byte> target) {
			Utf8.FromUtf16(str, target, out _, out var bytesWritten, true, true);
			if (bytesWritten > MaxStringBytes)
				throw new ArgumentException($"Name \"{str}\" is longer than {MaxStringBytes} bytes: {bytesWritten}");
		}

		private static readonly Encoding _utf8NoBom = new UTF8Encoding(
			encoderShouldEmitUTF8Identifier: false,
			throwOnInvalidBytes: true);

		public static RecordView<Raw.EpochHeader> CreateEpochRecord(
			DateTime timeStamp,
			long logPosition,
			int epochNumber,
			Guid epochId,
			long prevEpochPosition,
			Guid leaderInstanceId) {

			var record = MutableRecordView<Raw.EpochHeader>.Create(payloadLength: 0);
			ref var header = ref record.Header;
			ref var subHeader = ref record.SubHeader;

			header.Type = subHeader.Type();
			header.Version = subHeader.CurrentVersion();
			header.TimeStamp = timeStamp;
			header.RecordId = epochId;
			header.LogPosition = logPosition;

			subHeader.EpochNumber = epochNumber;
			subHeader.LeaderInstanceId = leaderInstanceId;
			subHeader.PrevEpochPosition = prevEpochPosition;

			return record;
		}

		public static StringPayloadRecord<Raw.PartitionHeader> CreatePartitionRecord(
			DateTime timeStamp,
			long logPosition,
			Guid partitionId,
			Guid partitionTypeId,
			Guid parentPartitionId,
			Raw.PartitionFlags flags,
			ushort referenceNumber,
			string name) {

			var payloadLength = _utf8NoBom.GetByteCount(name);
			var record = MutableRecordView<Raw.PartitionHeader>.Create(payloadLength);
			ref var header = ref record.Header;
			ref var subHeader = ref record.SubHeader;

			header.Type = subHeader.Type();
			header.Version = subHeader.CurrentVersion();
			header.TimeStamp = timeStamp;
			header.RecordId = partitionId;
			header.LogPosition = logPosition;

			subHeader.PartitionTypeId = partitionTypeId;
			subHeader.ParentPartitionId = parentPartitionId;
			subHeader.Flags = flags;
			subHeader.ReferenceNumber = referenceNumber;
			PopulateString(name, record.Payload.Span);

			return StringPayloadRecord.Create(record);
		}
		
		public static StringPayloadRecord<Raw.PartitionTypeHeader> CreatePartitionTypeRecord(
			DateTime timeStamp,
			long logPosition,
			Guid partitionTypeId,
			Guid partitionId,
			string name) {

			var payloadLength = _utf8NoBom.GetByteCount(name);
			var record = MutableRecordView<Raw.PartitionTypeHeader>.Create(payloadLength);
			ref var header = ref record.Header;
			ref var subHeader = ref record.SubHeader;

			header.Type = subHeader.Type();
			header.Version = subHeader.CurrentVersion();
			header.TimeStamp = timeStamp;
			header.RecordId = partitionTypeId;
			header.LogPosition = logPosition;

			subHeader.PartitionId = partitionId;
			PopulateString(name, record.Payload.Span);

			return StringPayloadRecord.Create(record);
		}

		public static StringPayloadRecord<Raw.StreamHeader> CreateStreamRecord(
			Guid streamId,
			DateTime timeStamp,
			long logPosition,
			uint streamNumber,
			string streamName,
			Guid partitionId,
			Guid streamTypeId) {

			var payloadLength = _utf8NoBom.GetByteCount(streamName);
			var record = MutableRecordView<Raw.StreamHeader>.Create(payloadLength);
			ref var header = ref record.Header;
			ref var subHeader = ref record.SubHeader;

			header.RecordId = streamId;
			header.Type = subHeader.Type();
			header.Version = subHeader.CurrentVersion();
			header.TimeStamp = timeStamp;
			header.LogPosition = logPosition;

			subHeader.PartitionId = partitionId;
			subHeader.StreamTypeId = streamTypeId;
			subHeader.ReferenceNumber = streamNumber;

			PopulateString(streamName, record.Payload.Span);

			return StringPayloadRecord.Create(record);
		}

		public static StringPayloadRecord<Raw.StreamTypeHeader> CreateStreamTypeRecord(
			DateTime timeStamp,
			long logPosition,
			Guid streamTypeId,
			Guid partitionId,
			string name) {

			var payloadLength = _utf8NoBom.GetByteCount(name);
			var record = MutableRecordView<Raw.StreamTypeHeader>.Create(payloadLength);
			ref var header = ref record.Header;
			ref var subHeader = ref record.SubHeader;

			header.Type = subHeader.Type();
			header.Version = subHeader.CurrentVersion();
			header.TimeStamp = timeStamp;
			header.RecordId = streamTypeId;
			header.LogPosition = logPosition;

			subHeader.PartitionId = partitionId;
			PopulateString(name, record.Payload.Span);

			return StringPayloadRecord.Create(record);
		}
		
		public static StringPayloadRecord<Raw.EventTypeHeader> CreateEventTypeRecord(
			DateTime timeStamp,
			long logPosition,
			Guid eventTypeId,
			Guid parentEventTypeId,
			Guid partitionId,
			uint referenceNumber,
			ushort version,
			string name) {

			var payloadLength = _utf8NoBom.GetByteCount(name);
			var record = MutableRecordView<Raw.EventTypeHeader>.Create(payloadLength);
			ref var header = ref record.Header;
			ref var subHeader = ref record.SubHeader;

			header.Type = subHeader.Type();
			header.Version = subHeader.CurrentVersion();
			header.TimeStamp = timeStamp;
			header.RecordId = eventTypeId;
			header.LogPosition = logPosition;

			subHeader.ParentEventTypeId = parentEventTypeId;
			subHeader.PartitionId = partitionId;
			subHeader.ReferenceNumber = referenceNumber;
			subHeader.Version = version;
			PopulateString(name, record.Payload.Span);

			return StringPayloadRecord.Create(record);
		}
		
		public static StringPayloadRecord<Raw.ContentTypeHeader> CreateContentTypeRecord(
			DateTime timeStamp,
			long logPosition,
			Guid contentTypeId,
			Guid partitionId,
			ushort referenceNumber,
			string name) {

			var payloadLength = _utf8NoBom.GetByteCount(name);
			var record = MutableRecordView<Raw.ContentTypeHeader>.Create(payloadLength);
			ref var header = ref record.Header;
			ref var subHeader = ref record.SubHeader;

			header.Type = subHeader.Type();
			header.Version = subHeader.CurrentVersion();
			header.TimeStamp = timeStamp;
			header.RecordId = contentTypeId;
			header.LogPosition = logPosition;

			subHeader.PartitionId = partitionId;
			subHeader.ReferenceNumber = referenceNumber;
			PopulateString(name, record.Payload.Span);

			return StringPayloadRecord.Create(record);
		}

		public static StreamWriteRecord CreateStreamWriteRecord(
			DateTime timeStamp,
			Guid correlationId,
			long logPosition,
			ushort prepareFlags,
			long streamNumber,
			long startingEventNumber,
			IEventRecord[] events) {
			var writeSystemMetadata = new StreamWriteSystemMetadata {
				CorrelationId = correlationId,
				StartingEventNumberRoot = 0,
				StartingEventNumberCategory = 0,
				PrepareFlags = ByteString.CopyFrom(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref prepareFlags, 1)))
			};
			var writeSystemMetadataSize = writeSystemMetadata.CalculateSize();

			var eventSystemMetadata = new EventSystemMetadata[events.Length];
			for (var i = 0; i < events.Length; i++) {
				eventSystemMetadata[i] = new EventSystemMetadata {
					EventId = events[i].EventId,
					EventType = events[i].EventType ?? string.Empty
				};
			}

			var payloadLength = MeasureWritePayload(writeSystemMetadataSize, events, eventSystemMetadata);
			var record = MutableRecordView<Raw.StreamWriteHeader>.Create(payloadLength);
			ref var header = ref record.Header;
			ref var recordId = ref record.RecordId<Raw.StreamWriteId>();
			ref var subHeader = ref record.SubHeader;

			header.Type = subHeader.Type();
			header.Version = subHeader.CurrentVersion();
			header.TimeStamp = timeStamp;
			header.LogPosition = logPosition;

			recordId.ParentTopicNumber = 0;
			recordId.TopicNumber = 0;
			recordId.CategoryNumber = 0;
			recordId.StreamNumber = (uint)streamNumber; // todo: remove cast
			recordId.StartingEventNumber = startingEventNumber;

			subHeader.Count = (short) events.Length;
			subHeader.Flags = 0;
			subHeader.MetadataSize = writeSystemMetadataSize;

			var slicer = record.Payload.Slicer();
			writeSystemMetadata.WriteTo(slicer.Slice(subHeader.MetadataSize).Span);

			for (var i = 0; i < events.Length; i++) {
				PopulateEventSubRecordOffsets(
					eventIndex: i,
					slicer: ref slicer);
				PopulateEventSubRecord(
					flags: (Raw.EventFlags) events[i].EventFlags,
					systemMetadataSize: eventSystemMetadata[i].CalculateSize(),
					systemMetadata: eventSystemMetadata[i],
					data: events[i].Data.Span,
					metadata: events[i].Metadata.Span,
					slicer: ref slicer);
			}

			return new StreamWriteRecord(record);

			static int MeasureWritePayload(int writeSystemMetadataSize,
				IEventRecord[] events,
				EventSystemMetadata[] eventSystemMetadata) {
				var result = writeSystemMetadataSize;
				for (var i = 0; i < events.Length; i++) {
					result += MeasureEventSubRecordOffsets(i);
					result += MeasureEventSubRecord(
						systemMetadataSize: eventSystemMetadata[i].CalculateSize(),
						data: events[i].Data.Span,
						metadata: events[i].Metadata.Span);
				}
				return result;
			}

			static int MeasureEventSubRecordOffsets(int eventIndex) {
				return eventIndex == 0 ? 0 : 2 * sizeof(int);
			}

			static int MeasureEventSubRecord(
				int systemMetadataSize,
				ReadOnlySpan<byte> data,
				ReadOnlySpan<byte> metadata) {

				return Raw.EventHeader.Size +
				       systemMetadataSize +
				       data.Length +
				       metadata.Length;
			}

			static void PopulateEventSubRecordOffsets(int eventIndex, ref MemorySlicer<byte> slicer) {
				if (eventIndex == 0) {
					return;
				}

				int forwardOffset = sizeof(int) /* log record length (prefix) */
									+ Raw.RecordHeader.Size /* log record header */
									+ Raw.StreamWriteHeader.Size /* stream write header */
									+ slicer.Offset /* current offset within payload */
									+ sizeof(int) /* first of the two event sub record offsets */;
				int backwardOffset = slicer.Remaining.Length /* remaining bytes till end of payload */
				                     - sizeof(int) /* exclude forward offset */
				                     + sizeof(int); /* log record length (suffix) */

				/* negate the offsets so that they are distinguishable from standard log record lengths */
				forwardOffset = -forwardOffset;
				backwardOffset = -backwardOffset;

				if (!BitConverter.IsLittleEndian) throw new NotSupportedException();
				var span = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref backwardOffset, 1));
				span.CopyTo(slicer.Slice(sizeof(int)).Span);
				span = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref forwardOffset, 1));
				span.CopyTo(slicer.Slice(sizeof(int)).Span);
			}

			static void PopulateEventSubRecord(
				Raw.EventFlags flags,
				int systemMetadataSize,
				EventSystemMetadata systemMetadata,
				ReadOnlySpan<byte> data,
				ReadOnlySpan<byte> metadata,
				ref MemorySlicer<byte> slicer) {
				ref var header = ref slicer.SliceAs<Raw.EventHeader>();
				header.EventTypeNumber = default;
				header.Flags = flags;
				header.EventSize = MeasureEventSubRecord(systemMetadataSize, data, metadata);
				header.SystemMetadataSize = systemMetadataSize;
				header.DataSize = data.Length;

				systemMetadata.WriteTo(slicer.Slice(systemMetadataSize).Span);
				data.CopyTo(slicer.Slice(header.DataSize).Span);
				metadata.CopyTo(slicer.Slice(metadata.Length).Span);
			}
		}

		public static RecordView<Raw.TransactionStartHeader> CreateTransactionStartRecord(
			DateTime timeStamp,
			long logPosition,
			Guid transactionId,
			Raw.TransactionStatus status,
			Raw.TransactionType type,
			uint recordCount) {

			var record = MutableRecordView<Raw.TransactionStartHeader>.Create(payloadLength: 0);
			ref var header = ref record.Header;
			ref var subHeader = ref record.SubHeader;
			
			header.Type = subHeader.Type();
			header.Version = subHeader.CurrentVersion();
			header.TimeStamp = timeStamp;
			header.RecordId = transactionId;
			header.LogPosition = logPosition;

			subHeader.Status = status;
			subHeader.Type = type;
			subHeader.RecordCount = recordCount;

			return record;
		}
		
		public static RecordView<Raw.TransactionEndHeader> CreateTransactionEndRecord(
			DateTime timeStamp,
			long logPosition,
			Guid transactionId,
			uint recordCount) {

			var record = MutableRecordView<Raw.TransactionEndHeader>.Create(payloadLength: 0);
			ref var header = ref record.Header;
			ref var subHeader = ref record.SubHeader;

			header.Type = subHeader.Type();
			header.Version = subHeader.CurrentVersion();
			header.TimeStamp = timeStamp;
			header.RecordId = transactionId;
			header.LogPosition = logPosition;
			
			subHeader.RecordCount = recordCount;

			return record;
		}
	}
}
