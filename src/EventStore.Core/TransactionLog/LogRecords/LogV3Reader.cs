using System;
using System.IO;
using EventStore.LogCommon;
using EventStore.LogV3;

namespace EventStore.Core.TransactionLog.LogRecords {
	public static class LogV3Reader {
		public static LogV3EpochLogRecord ReadEpoch(LogRecordType type, byte version, BinaryReader reader) {
			// temporary: we happen to know the length of an epoch, its always the same.
			// later we will pass the size in since we will need it for dynamically sized records.
			var length = Raw.RecordHeader.Size + Raw.EpochHeader.Size;
			var bytes = ReadBytes(type, version, reader, length);
			return new LogV3EpochLogRecord(bytes);
		}

		private static byte[] ReadBytes(LogRecordType type, byte version, BinaryReader reader, int recordLength) {
			// todo: if we could get some confidence that we would return to the pool
			// (e.g. with reference counting) then we could use arraypool here. or just maybe a ring buffer
			// var bytes = ArrayPool<byte>.Shared.Rent(length);
			var bytes = new byte[recordLength];
			bytes[0] = (byte)type;
			bytes[1] = version;
			reader.Read(bytes.AsSpan(2..recordLength));
			return bytes;
		}
	}
}
