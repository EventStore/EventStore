using System;
using System.IO;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.LogAbstraction;
using EventStore.Core.Services;
using EventStore.LogCommon;

namespace EventStore.Core.TransactionLog.LogRecords {
	public abstract class LogRecord : ILogRecord {
		public static readonly ReadOnlyMemory<byte> NoData = Empty.ByteArray;

		public LogRecordType RecordType { get; }
		public byte Version { get; }
		public long LogPosition { get; }

		public long GetNextLogPosition(long logicalPosition, int length) {
			return logicalPosition + length + 2 * sizeof(int);
		}

		public long GetPrevLogPosition(long logicalPosition, int length) {
			return logicalPosition - length - 2 * sizeof(int);
		}

		public static ILogRecord ReadFrom(BinaryReader reader, int length) {
			var recordType = (LogRecordType)reader.ReadByte();
			var version = reader.ReadByte();

			static long ReadPosition(BinaryReader reader) {
				var logPosition = reader.ReadInt64();
				Ensure.Nonnegative(logPosition, "logPosition");
				return logPosition;
			}

			switch (recordType) {
				case LogRecordType.Prepare:
					return new PrepareLogRecord(reader, version, ReadPosition(reader));
				case LogRecordType.Commit:
					return new CommitLogRecord(reader, version, ReadPosition(reader));
				case LogRecordType.System:
					if (version > SystemLogRecord.SystemRecordVersion)
						return new LogV3EpochLogRecord(LogV3Reader.ReadBytes(recordType, version, reader, length));

					return new SystemLogRecord(reader, version, ReadPosition(reader));

				case LogRecordType.StreamWrite:
					return new LogV3StreamWriteRecord(LogV3Reader.ReadBytes(recordType, version, reader, length));

				case LogRecordType.Stream:
					return new LogV3StreamRecord(LogV3Reader.ReadBytes(recordType, version, reader, length));

				case LogRecordType.EventType:
					return new LogV3EventTypeRecord(LogV3Reader.ReadBytes(recordType, version, reader, length));

				case LogRecordType.PartitionType:
					return new PartitionTypeLogRecord(LogV3Reader.ReadBytes(recordType, version, reader, length));
				
				case LogRecordType.Partition:
					return new PartitionLogRecord(LogV3Reader.ReadBytes(recordType, version, reader, length));
				
				default:
					throw new ArgumentOutOfRangeException("recordType");
			}
		}

		// used by tests only
		public static IPrepareLogRecord<TStreamId> Prepare<TStreamId>(IRecordFactory<TStreamId> factory, long logPosition, Guid correlationId, Guid eventId, long transactionPos,
			int transactionOffset, TStreamId eventStreamId, long expectedVersion, PrepareFlags flags,
			TStreamId eventType, ReadOnlyMemory<byte> data, ReadOnlyMemory<byte> metadata, DateTime? timeStamp = null) {
			return Prepare(
				factory: factory,
				logPosition: logPosition,
				correlationId: correlationId,
				eventId: eventId,
				transactionPos: transactionPos,
				transactionOffset: transactionOffset,
				eventStreamId: eventStreamId,
				eventStreamIdSize: null,
				expectedVersion: expectedVersion,
				flags: flags,
				eventType: eventType,
				eventTypeSize: null,
				data: data,
				metadata: metadata,
				timeStamp: timeStamp);
		}

		public static IPrepareLogRecord<TStreamId> Prepare<TStreamId>(IRecordFactory<TStreamId> factory, long logPosition, Guid correlationId, Guid eventId, long transactionPos,
			int transactionOffset,
			TStreamId eventStreamId, int? eventStreamIdSize, long expectedVersion, PrepareFlags flags,
			TStreamId eventType, int? eventTypeSize,
			ReadOnlyMemory<byte> data, ReadOnlyMemory<byte> metadata, DateTime? timeStamp = null) {
			return factory.CreatePrepare(logPosition, correlationId, eventId, transactionPos, transactionOffset,
				eventStreamId, eventStreamIdSize, expectedVersion, timeStamp ?? DateTime.UtcNow, flags, eventType, eventTypeSize,
				data, metadata);
		}

		public static CommitLogRecord Commit(long logPosition, Guid correlationId, long startPosition,
			long eventNumber) {
			return new CommitLogRecord(logPosition, correlationId, startPosition, DateTime.UtcNow, eventNumber);
		}

		// used by tests only
		public static IPrepareLogRecord<TStreamId> SingleWrite<TStreamId>(IRecordFactory<TStreamId> factory, long logPosition, Guid correlationId, Guid eventId,
			TStreamId eventStreamId, long expectedVersion, TStreamId eventType, ReadOnlyMemory<byte> data, ReadOnlyMemory<byte> metadata,
			DateTime? timestamp = null, PrepareFlags? additionalFlags = null) {
			return factory.CreatePrepare(logPosition, correlationId, eventId, logPosition, 0, eventStreamId, eventStreamIdSize: null,
				expectedVersion, timestamp ?? DateTime.UtcNow,
				PrepareFlags.Data | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd | (additionalFlags ?? PrepareFlags.None),
				eventType, eventTypeSize: null, data, metadata);
		}

		public static IPrepareLogRecord<TStreamId> TransactionBegin<TStreamId>(IRecordFactory<TStreamId> factory, long logPos, Guid correlationId, TStreamId eventStreamId,
			int? eventStreamIdSize, long expectedVersion) {
			return factory.CreatePrepare(logPos, correlationId, Guid.NewGuid(), logPos, -1,
				eventStreamId, eventStreamIdSize, expectedVersion, DateTime.UtcNow, PrepareFlags.TransactionBegin,
				default, 0, NoData, NoData);
		}

		public static IPrepareLogRecord<TStreamId> TransactionWrite<TStreamId>(IRecordFactory<TStreamId> factory, long logPosition, Guid correlationId, Guid eventId,
			long transactionPos, int transactionOffset, TStreamId eventStreamId, int? eventStreamIdSize, TStreamId eventType, int? eventTypeSize,
			byte[] data, byte[] metadata, bool isJson) {
			return factory.CreatePrepare(logPosition, correlationId, eventId, transactionPos, transactionOffset,
				eventStreamId, eventStreamIdSize, ExpectedVersion.Any, DateTime.UtcNow,
				PrepareFlags.Data | (isJson ? PrepareFlags.IsJson : PrepareFlags.None),
				eventType, eventTypeSize, data, metadata);
		}

		public static IPrepareLogRecord<TStreamId> TransactionEnd<TStreamId>(IRecordFactory<TStreamId> factory, long logPos, Guid correlationId, Guid eventId,
			long transactionPos, TStreamId eventStreamId, int? eventStreamIdSize) {
			return factory.CreatePrepare(logPos, correlationId, eventId, transactionPos, -1, eventStreamId,
				eventStreamIdSize, ExpectedVersion.Any, DateTime.UtcNow, PrepareFlags.TransactionEnd,
				default, 0, NoData, NoData);
		}

		public static IPrepareLogRecord<TStreamId> DeleteTombstone<TStreamId>(IRecordFactory<TStreamId> factory, long logPosition, Guid correlationId, Guid eventId,
			TStreamId eventStreamId, int? eventStreamIdSize, TStreamId eventType, int? eventTypeSize, long expectedVersion, PrepareFlags additionalFlags = PrepareFlags.None) {
			return factory.CreatePrepare(logPosition, correlationId, eventId, logPosition, 0,
				eventStreamId, eventStreamIdSize, expectedVersion, DateTime.UtcNow,
				PrepareFlags.StreamDelete | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd |
				additionalFlags, eventType, eventTypeSize, NoData, NoData);
		}

		protected LogRecord(LogRecordType recordType, byte version, long logPosition) {
			Ensure.Nonnegative(logPosition, "logPosition");
			RecordType = recordType;
			Version = version;
			LogPosition = logPosition;
		}

		public virtual void WriteTo(BinaryWriter writer) {
			writer.Write((byte)RecordType);
			writer.Write(Version);
			writer.Write(LogPosition);
		}

		public int GetSizeWithLengthPrefixAndSuffix() {
			using (var memoryStream = new MemoryStream()) {
				WriteTo(new BinaryWriter(memoryStream));
				return 8 + (int)memoryStream.Length;
			}
		}
	}
}
