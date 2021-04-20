using System.IO;
using EventStore.LogCommon;

namespace EventStore.Core.TransactionLog.LogRecords {
	public interface ILogRecord {
		LogRecordType RecordType { get; }
		byte Version { get; }
		public long LogPosition { get; }
		void WriteTo(BinaryWriter writer);
		//qq what are the actual required semantics here? look at the calling sites
		// this is also an asymmetry in the calling between getnext and getprev.
		long GetNextLogPosition(long logicalPosition, int length);
		long GetPrevLogPosition(long logicalPosition, int length);
		int GetSizeWithLengthPrefixAndSuffix();
	}
}
