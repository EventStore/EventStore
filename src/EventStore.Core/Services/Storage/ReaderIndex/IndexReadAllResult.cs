using System.Collections.Generic;
using System.Linq;
using EventStore.Common.Utils;
using EventStore.Core.TransactionLogV2.Data;

namespace EventStore.Core.Services.Storage.ReaderIndex {
	public struct IndexReadAllResult {
		public readonly List<CommitEventRecord> Records;
		public readonly TFPos CurrentPos;
		public readonly TFPos NextPos;
		public readonly TFPos PrevPos;
		public readonly bool IsEndOfStream;
		public readonly long ConsideredEventsCount;

		public IndexReadAllResult(List<CommitEventRecord> records, TFPos currentPos, TFPos nextPos, TFPos prevPos,
			bool isEndOfStream, long consideredEventsCount) {
			Ensure.NotNull(records, "records");

			Records = records;
			CurrentPos = currentPos;
			NextPos = nextPos;
			PrevPos = prevPos;
			IsEndOfStream = isEndOfStream;
			ConsideredEventsCount = consideredEventsCount;
		}

		public override string ToString() {
			return string.Format("CurrentPos: {0}, NextPos: {1}, PrevPos: {2}, IsEndOfStream: {3}, Records: {4}",
				CurrentPos, NextPos, PrevPos, IsEndOfStream, string.Join("\n", Records.Select(x => x.ToString())));
		}
	}
}
