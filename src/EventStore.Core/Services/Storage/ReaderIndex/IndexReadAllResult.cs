using System.Collections.Generic;
using System.Linq;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.TransactionLog.Data;

namespace EventStore.Core.Services.Storage.ReaderIndex {
	public struct IndexReadAllResult {
		public readonly List<CommitEventRecord> Records;
		public readonly TFPos CurrentPos;
		public readonly TFPos NextPos;
		public readonly TFPos PrevPos;
		public readonly bool IsEndOfStream;

		public IndexReadAllResult(List<CommitEventRecord> records, TFPos currentPos, TFPos nextPos, TFPos prevPos, bool isEndOfStream) {
			Ensure.NotNull(records, "records");

			Records = records;
			CurrentPos = currentPos;
			NextPos = nextPos;
			PrevPos = prevPos;
			IsEndOfStream = isEndOfStream;
		}

		public override string ToString() {
			return string.Format("CurrentPos: {0}, NextPos: {1}, PrevPos: {2}, IsEndOfStream: {3}, Records: {4}",
				CurrentPos, NextPos, PrevPos, string.Join("\n", IsEndOfStream, Records.Select(x => x.ToString())));
		}
	}
}
