using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog {
	public struct RecordReadResult {
		public static readonly RecordReadResult Failure = new RecordReadResult(false, -1, null, 0);

		public readonly bool Success;
		public readonly long NextPosition;
		public readonly LogRecord LogRecord;
		public readonly int RecordLength;

		public RecordReadResult(bool success, long nextPosition, LogRecord logRecord, int recordLength) {
			Success = success;
			LogRecord = logRecord;
			NextPosition = nextPosition;
			RecordLength = recordLength;
		}

		public override string ToString() {
			return string.Format("Success: {0}, NextPosition: {1}, RecordLength: {2}, LogRecord: {3}",
				Success,
				NextPosition,
				RecordLength,
				LogRecord);
		}
	}

	public struct SeqReadResult {
		public static readonly SeqReadResult Failure = new SeqReadResult(false, true, null, 0, -1, -1);

		public readonly bool Success;
		public readonly bool Eof;
		public readonly LogRecord LogRecord;
		public readonly int RecordLength;
		public readonly long RecordPrePosition;
		public readonly long RecordPostPosition;

		public SeqReadResult(bool success,
			bool eof,
			LogRecord logRecord,
			int recordLength,
			long recordPrePosition,
			long recordPostPosition) {
			Success = success;
			Eof = eof;
			LogRecord = logRecord;
			RecordLength = recordLength;
			RecordPrePosition = recordPrePosition;
			RecordPostPosition = recordPostPosition;
		}

		public override string ToString() {
			return string.Format(
				"Success: {0}, RecordLength: {1}, RecordPrePosition: {2}, RecordPostPosition: {3}, LogRecord: {4}",
				Success,
				RecordLength,
				RecordPrePosition,
				RecordPostPosition,
				LogRecord);
		}
	}
}
