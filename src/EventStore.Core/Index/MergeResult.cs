using System.Collections.Generic;

namespace EventStore.Core.Index {
	public class MergeResult {
		public readonly IndexMap MergedMap;
		public readonly List<PTable> ToDelete;

		public MergeResult(IndexMap mergedMap, List<PTable> toDelete) {
			MergedMap = mergedMap;
			ToDelete = toDelete;
		}
	}
}
