﻿using System;
using System.Text;
using System.Text.Unicode;
using EventStore.LogCommon;

namespace EventStore.LogV3 {
	// Nice-to-use wrappers for creating and populating the raw structures.
	public static class RecordCreator {
		// these exist to make the code more obviously correct
		private static byte CurrentVersion(this ref Raw.EpochHeader _) => LogRecordVersion.LogRecordV1;
		private static byte CurrentVersion(this ref Raw.PartitionHeader _) => LogRecordVersion.LogRecordV0;
		private static byte CurrentVersion(this ref Raw.PartitionTypeHeader _) => LogRecordVersion.LogRecordV0;
		private static byte CurrentVersion(this ref Raw.StreamTypeHeader _) => LogRecordVersion.LogRecordV0;
		private static byte CurrentVersion(this ref Raw.EventTypeHeader _) => LogRecordVersion.LogRecordV0;
		private static byte CurrentVersion(this ref Raw.ContentTypeHeader _) => LogRecordVersion.LogRecordV0;

		private static LogRecordType Type(this ref Raw.EpochHeader _) => LogRecordType.System;
		private static LogRecordType Type(this ref Raw.PartitionHeader _) => LogRecordType.Partition;
		private static LogRecordType Type(this ref Raw.PartitionTypeHeader _) => LogRecordType.PartitionType;
		private static LogRecordType Type(this ref Raw.StreamTypeHeader _) => LogRecordType.StreamType;
		private static LogRecordType Type(this ref Raw.EventTypeHeader _) => LogRecordType.EventType;
		private static LogRecordType Type(this ref Raw.ContentTypeHeader _) => LogRecordType.ContentType;
		const int MaxStringBytes = 100;
		static void PopulateString(string str, Memory<byte> target) {
			Utf8.FromUtf16(str, target.Span, out _, out var bytesWritten, true, true);
			if (bytesWritten > MaxStringBytes)
				throw new ArgumentException($"Name \"{str}\" is longer than {MaxStringBytes} bytes");
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
			byte flags,
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
			PopulateString(name, record.Payload);

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
			PopulateString(name, record.Payload);

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
			PopulateString(name, record.Payload);

			return StringPayloadRecord.Create(record);
		}
		
		public static StringPayloadRecord<Raw.EventTypeHeader> CreateEventTypeRecord(
			DateTime timeStamp,
			long logPosition,
			Guid eventTypeId,
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

			subHeader.PartitionId = partitionId;
			subHeader.ReferenceNumber = referenceNumber;
			subHeader.Version = version;
			PopulateString(name, record.Payload);

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
			PopulateString(name, record.Payload);

			return StringPayloadRecord.Create(record);
		}
	}
}
