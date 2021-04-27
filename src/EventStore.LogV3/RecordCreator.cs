using System;
using System.Text;
using System.Text.Unicode;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.LogCommon;

namespace EventStore.LogV3 {
	// Nice-to-use wrappers for creating and populating the raw structures.
	public static class RecordCreator {
		// these exist to make the code more obviously correct
		private static byte CurrentVersion(this ref Raw.EpochHeader _) => LogRecordVersion.LogRecordV1;
		private static byte CurrentVersion(this ref Raw.PartitionTypeHeader _) => LogRecordVersion.LogRecordV0;
		private static byte CurrentVersion(this ref Raw.StreamHeader _) => LogRecordVersion.LogRecordV0;
		private static byte CurrentVersion(this ref Raw.StreamTypeHeader _) => LogRecordVersion.LogRecordV0;
		private static byte CurrentVersion(this ref Raw.StreamWriteHeader _) => LogRecordVersion.LogRecordV0;

		private static LogRecordType Type(this ref Raw.EpochHeader _) => LogRecordType.System;
		private static LogRecordType Type(this ref Raw.PartitionTypeHeader _) => LogRecordType.PartitionType;
		private static LogRecordType Type(this ref Raw.StreamHeader _) => LogRecordType.Stream;
		private static LogRecordType Type(this ref Raw.StreamTypeHeader _) => LogRecordType.StreamType;
		private static LogRecordType Type(this ref Raw.StreamWriteHeader _) => LogRecordType.LogV3StreamWrite;

		const int MaxStringBytes = 100;
		public static void PopulateString(string str, Span<byte> target) {
			Utf8.FromUtf16(str, target, out _, out var bytesWritten, true, true);
			if (bytesWritten > MaxStringBytes)
				throw new ArgumentException($"Name \"{str}\" is longer than 100 bytes");
		}

		public static int MeasureString(string str) {
			return _utf8NoBom.GetByteCount(str);
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
			long streamNumber,
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
			subHeader.ReferenceId = streamNumber;

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

		public static StreamWriteRecord CreateStreamWriteRecordForSingleEvent(
			DateTime timeStamp,
			Guid recordId,
			long logPosition,
			long streamNumber,
			long startingEventNumber,
			ReadOnlySpan<byte> recordMetadata,
			Guid eventId,
			ReadOnlySpan<byte> eventSystemMetadata,
			ReadOnlySpan<byte> eventData,
			ReadOnlySpan<byte> eventMetadata,
			PrepareFlags eventFlags) {

			var payloadLength = MeasureWritePayload(recordMetadata, eventSystemMetadata, eventData, eventMetadata);

			var record = MutableRecordView<Raw.StreamWriteHeader>.Create(payloadLength);
			ref var header = ref record.Header;
			ref var subHeader = ref record.SubHeader;

			header.Type = subHeader.Type();
			header.Version = subHeader.CurrentVersion();
			header.TimeStamp = timeStamp;
			header.RecordId = recordId;
			header.LogPosition = logPosition;

			subHeader.Count = 1;
			subHeader.StreamNumber = streamNumber;
			subHeader.StartingEventNumber = startingEventNumber;
			subHeader.Flags = 0;
			subHeader.MetadataSize = recordMetadata.Length;

			var slicer = record.Payload.Slicer();
			recordMetadata.CopyTo(slicer.Slice(subHeader.MetadataSize).Span);

			PopulateEventSubRecord(
				eventId: eventId,
				flags: eventFlags,
				systemMetadata: eventSystemMetadata,
				data: eventData,
				metadata: eventMetadata,
				target: slicer.Remaining);

			return new StreamWriteRecord(record);

			static int MeasureWritePayload(
				ReadOnlySpan<byte> recordMetadata,
				ReadOnlySpan<byte> eventSystemMetadata,
				ReadOnlySpan<byte> userData,
				ReadOnlySpan<byte> userMetadata) {

				var result =
					recordMetadata.Length +
					MeasureForEventSubRecord(
						systemMetadata: eventSystemMetadata,
						data: userData,
						metadata: userMetadata);
				return result;
			}

			static int MeasureForEventSubRecord(
				ReadOnlySpan<byte> systemMetadata,
				ReadOnlySpan<byte> data,
				ReadOnlySpan<byte> metadata) {

				return
					Raw.EventHeader.Size +
					systemMetadata.Length +
					data.Length +
					metadata.Length;
			}

			static void PopulateEventSubRecord(
				Guid eventId,
				PrepareFlags flags,
				ReadOnlySpan<byte> systemMetadata,
				ReadOnlySpan<byte> data,
				ReadOnlySpan<byte> metadata,
				Memory<byte> target) {

				var slicer = target.Slicer();
				ref var header = ref slicer.SliceAs<Raw.EventHeader>();
				header.EventTypeNumber = default;
				header.Flags = flags;
				header.EventSize = MeasureForEventSubRecord(systemMetadata, data, metadata);
				header.SystemMetadataSize = systemMetadata.Length;
				header.DataSize = data.Length;
				header.EventId = eventId;

				systemMetadata.CopyTo(slicer.Slice(header.SystemMetadataSize).Span);
				data.CopyTo(slicer.Slice(header.DataSize).Span);
				metadata.CopyTo(slicer.Remaining.Span);
			}
		}
	}
}
