using System;
using System.IO;
using System.Linq;
using EventStore.Common.Utils;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.Helpers;

namespace EventStore.Core.TransactionLog.LogRecords {
	[Flags]
	public enum PrepareFlags : ushort {
		None = 0x00,
		Data = 0x01, // prepare contains data
		TransactionBegin = 0x02, // prepare starts transaction
		TransactionEnd = 0x04, // prepare ends transaction
		StreamDelete = 0x08, // prepare deletes stream

		IsCommitted = 0x20, // prepare should be considered committed immediately, no commit will follow in TF
		//Update = 0x30,                  // prepare updates previous instance of the same event, DANGEROUS!

		IsJson = 0x100, // indicates data & metadata are valid json

		// aggregate flag set
		DeleteTombstone = TransactionBegin | TransactionEnd | StreamDelete,
		SingleWrite = Data | TransactionBegin | TransactionEnd
	}

	public static class PrepareFlagsExtensions {
		public static bool HasAllOf(this PrepareFlags flags, PrepareFlags flagSet) {
			return (flags & flagSet) == flagSet;
		}

		public static bool HasAnyOf(this PrepareFlags flags, PrepareFlags flagSet) {
			return (flags & flagSet) != 0;
		}

		public static bool HasNoneOf(this PrepareFlags flags, PrepareFlags flagSet) {
			return (flags & flagSet) == 0;
		}
	}

	public class PrepareLogRecord : LogRecord, IEquatable<PrepareLogRecord> {
		public const byte PrepareRecordVersion = 1;

		public readonly PrepareFlags Flags;
		public readonly long TransactionPosition;
		public readonly int TransactionOffset;
		public readonly long ExpectedVersion; // if IsCommitted is set, this is final EventNumber
		public readonly string EventStreamId;

		public readonly Guid EventId;
		public readonly Guid CorrelationId;
		public readonly DateTime TimeStamp;
		public readonly string EventType;
		public readonly byte[] Data;
		public readonly byte[] Metadata;

		public long InMemorySize {
			get {
				return sizeof(LogRecordType)
				       + 1
				       + 8
				       + sizeof(PrepareFlags)
				       + 8
				       + 4
				       + 4
				       + IntPtr.Size + EventStreamId.Length * 2
				       + 16
				       + 16
				       + 8
				       + IntPtr.Size + EventType.Length * 2
				       + IntPtr.Size + Data.Length
				       + IntPtr.Size + Metadata.Length;
			}
		}

		public PrepareLogRecord(long logPosition,
			Guid correlationId,
			Guid eventId,
			long transactionPosition,
			int transactionOffset,
			string eventStreamId,
			long expectedVersion,
			DateTime timeStamp,
			PrepareFlags flags,
			string eventType,
			byte[] data,
			byte[] metadata,
			byte prepareRecordVersion = PrepareRecordVersion)
			: base(LogRecordType.Prepare, prepareRecordVersion, logPosition) {
			Ensure.NotEmptyGuid(correlationId, "correlationId");
			Ensure.NotEmptyGuid(eventId, "eventId");
			Ensure.Nonnegative(transactionPosition, "transactionPosition");
			if (transactionOffset < -1)
				throw new ArgumentOutOfRangeException("transactionOffset");
			Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
			if (expectedVersion < Core.Data.ExpectedVersion.Any)
				throw new ArgumentOutOfRangeException("expectedVersion");
			Ensure.NotNull(data, "data");

			Flags = flags;
			TransactionPosition = transactionPosition;
			TransactionOffset = transactionOffset;
			ExpectedVersion = expectedVersion;
			EventStreamId = eventStreamId;

			EventId = eventId;
			CorrelationId = correlationId;
			TimeStamp = timeStamp;
			EventType = eventType ?? string.Empty;
			Data = data;
			Metadata = metadata ?? NoData;
			if (InMemorySize > TFConsts.MaxLogRecordSize) throw new Exception("Record too large.");
		}

		internal PrepareLogRecord(BinaryReader reader, byte version, long logPosition) : base(LogRecordType.Prepare,
			version, logPosition) {
			if (version != LogRecordVersion.LogRecordV0 && version != LogRecordVersion.LogRecordV1)
				throw new ArgumentException(string.Format(
					"PrepareRecord version {0} is incorrect. Supported version: {1}.", version, PrepareRecordVersion));

			Flags = (PrepareFlags)reader.ReadUInt16();
			TransactionPosition = reader.ReadInt64();
			TransactionOffset = reader.ReadInt32();
			ExpectedVersion = version == LogRecordVersion.LogRecordV0 ? reader.ReadInt32() : reader.ReadInt64();

			if (version == LogRecordVersion.LogRecordV0) {
				ExpectedVersion = ExpectedVersion == int.MaxValue - 1 ? long.MaxValue - 1 : ExpectedVersion;
			}

			EventStreamId = reader.ReadString();
			EventId = new Guid(reader.ReadBytes(16));
			CorrelationId = new Guid(reader.ReadBytes(16));
			TimeStamp = new DateTime(reader.ReadInt64());
			EventType = reader.ReadString();

			var dataCount = reader.ReadInt32();
			Data = dataCount == 0 ? NoData : reader.ReadBytes(dataCount);

			var metadataCount = reader.ReadInt32();
			Metadata = metadataCount == 0 ? NoData : reader.ReadBytes(metadataCount);
			if (InMemorySize > TFConsts.MaxLogRecordSize) throw new Exception("Record too large.");
		}

		public override void WriteTo(BinaryWriter writer) {
			base.WriteTo(writer);

			writer.Write((ushort)Flags);
			writer.Write(TransactionPosition);
			writer.Write(TransactionOffset);
			if (Version == LogRecordVersion.LogRecordV0) {
				int expectedVersion = ExpectedVersion == long.MaxValue - 1 ? int.MaxValue - 1 : (int)ExpectedVersion;
				writer.Write(expectedVersion);
			} else {
				writer.Write(ExpectedVersion);
			}

			writer.Write(EventStreamId);

			writer.Write(EventId.ToByteArray());
			writer.Write(CorrelationId.ToByteArray());
			writer.Write(TimeStamp.Ticks);
			writer.Write(EventType);
			writer.Write(Data.Length);
			writer.Write(Data);
			writer.Write(Metadata.Length);
			writer.Write(Metadata);
		}

		public bool Equals(PrepareLogRecord other) {
			if (ReferenceEquals(null, other)) return false;
			if (ReferenceEquals(this, other)) return true;
			return other.LogPosition == LogPosition
			       && other.Flags == Flags
			       && other.TransactionPosition == TransactionPosition
			       && other.TransactionOffset == TransactionOffset
			       && other.ExpectedVersion == ExpectedVersion
			       && other.EventStreamId.Equals(EventStreamId)
			       && other.EventId == EventId
			       && other.CorrelationId == CorrelationId
			       && other.TimeStamp.Equals(TimeStamp)
			       && other.EventType.Equals(EventType)
			       && other.Data.SequenceEqual(Data)
			       && other.Metadata.SequenceEqual(Metadata);
		}

		public override bool Equals(object obj) {
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != typeof(PrepareLogRecord)) return false;
			return Equals((PrepareLogRecord)obj);
		}

		public override int GetHashCode() {
			unchecked {
				int result = LogPosition.GetHashCode();
				result = (result * 397) ^ Flags.GetHashCode();
				result = (result * 397) ^ TransactionPosition.GetHashCode();
				result = (result * 397) ^ TransactionOffset;
				result = (result * 397) ^ ExpectedVersion.GetHashCode();
				result = (result * 397) ^ EventStreamId.GetHashCode();

				result = (result * 397) ^ EventId.GetHashCode();
				result = (result * 397) ^ CorrelationId.GetHashCode();
				result = (result * 397) ^ TimeStamp.GetHashCode();
				result = (result * 397) ^ EventType.GetHashCode();
				result = (result * 397) ^ Data.GetHashCode();
				result = (result * 397) ^ Metadata.GetHashCode();
				return result;
			}
		}

		public static bool operator ==(PrepareLogRecord left, PrepareLogRecord right) {
			return Equals(left, right);
		}

		public static bool operator !=(PrepareLogRecord left, PrepareLogRecord right) {
			return !Equals(left, right);
		}

		public override string ToString() {
			return string.Format("LogPosition: {0}, "
			                     + "Flags: {1}, "
			                     + "TransactionPosition: {2}, "
			                     + "TransactionOffset: {3}, "
			                     + "ExpectedVersion: {4}, "
			                     + "EventStreamId: {5}, "
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
	}
}
