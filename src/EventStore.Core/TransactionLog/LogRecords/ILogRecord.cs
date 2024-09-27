using System.IO;
using EventStore.LogCommon;

namespace EventStore.Core.TransactionLog.LogRecords {
	public interface ILogRecord {
		LogRecordType RecordType { get; }
		byte Version { get; }
		public long LogPosition { get; }
		void WriteTo(BinaryWriter writer);
		long GetNextLogPosition(long logicalPosition, int length);
		long GetPrevLogPosition(long logicalPosition, int length);
		int GetSizeWithLengthPrefixAndSuffix();
	}
}
