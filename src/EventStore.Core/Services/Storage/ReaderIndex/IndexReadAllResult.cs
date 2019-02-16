using System.Collections.Generic;
using System.Linq;
using EventStore.Common.Utils;
using EventStore.Core.Data;

namespace EventStore.Core.Services.Storage.ReaderIndex {
	public struct IndexReadAllResult {
		public readonly List<CommitEventRecord> Records;
		public readonly TFPos CurrentPos;
		public readonly TFPos NextPos;
		public readonly TFPos PrevPos;

		public IndexReadAllResult(List<CommitEventRecord> records, TFPos currentPos, TFPos nextPos, TFPos prevPos) {
			Ensure.NotNull(records, "records");

			Records = records;
			CurrentPos = currentPos;
			NextPos = nextPos;
			PrevPos = prevPos;
		}

		public override string ToString() {
			return string.Format("CurrentPos: {0}, NextPos: {1}, PrevPos: {2}, Records: {3}",
				CurrentPos, NextPos, PrevPos, string.Join("\n", Records.Select(x => x.ToString())));
		}
	}
}
