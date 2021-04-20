using System;
using System.IO;
using EventStore.LogCommon;

namespace EventStore.Core.TransactionLog.LogRecords {
	public static class LogV3Reader {
		//qq we either want to return the whole record, or a subrecord.
		// if subrecordoffset is 0 (which will be the case for replication, $all reads etc), then we want to get the whole record.
		// if it is greater than 0 then it is the offset of the subrecord that we want in its main record.
		//    (i.e. the amount we had to move back in order to get to the record start (//qqqqqq wait, including the size framing?)
		//public static ILogRecord ReadFrom(BinaryReader reader, int length, int subrecordOffset, out int lengthOut) {
		//	lengthOut = length;
		public static byte[] ReadBytes(LogRecordType type, byte version, BinaryReader reader, int recordLength) {
			// todo: if we could get some confidence that we would return to the pool
			// (e.g. with reference counting) then we could use arraypool here. or just maybe a ring buffer
			// var bytes = ArrayPool<byte>.Shared.Rent(length);
			var bytes = new byte[recordLength];
			bytes[0] = (byte)type;
			bytes[1] = version;
			//qq if we wanna shortcut this with a cache, do it here or before. we might still need to advance the reader.
			reader.Read(bytes.AsSpan(2..recordLength));
			return bytes;
		}
	}
}
