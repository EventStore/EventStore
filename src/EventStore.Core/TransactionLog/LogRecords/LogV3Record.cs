using System;
using System.IO;
using EventStore.LogCommon;
using EventStore.LogV3;

namespace EventStore.Core.TransactionLog.LogRecords {
	// This is the adapter to plug V3 records into the standard machinery.
	public class LogV3Record<TRecordView> : ILogRecord where TRecordView : IRecordView {
		public TRecordView Record { get; init; }

		public long GetNextLogPosition(long logicalPosition, int length) {
			return logicalPosition + length + 2 * sizeof(int);
		}

		public long GetPrevLogPosition(long logicalPosition, int length) {
			return logicalPosition - length - 2 * sizeof(int);
		}

		// probably only needs to be virtual temporarily
		public virtual LogRecordType RecordType => Record.Header.Type;

		public byte Version => Record.Header.Version;

		public long LogPosition => Record.Header.LogPosition;

		public DateTime TimeStamp => Record.Header.TimeStamp;

		public LogV3Record() {
		}

		public void WriteTo(BinaryWriter writer) {
			writer.Write(Record.Bytes.Span);
		}

		public int GetSizeWithLengthPrefixAndSuffix() {
			return 2 * sizeof(int) + Record.Bytes.Length;
		}
	}
}
