using System.Collections.Generic;

namespace EventStore.Core.Index {
	public class MergeResult {
		public readonly IndexMap MergedMap;
		public readonly List<PTable> ToDelete;
		public readonly bool HasMergedAny;
		public readonly bool CanMergeAny;

		public MergeResult(IndexMap mergedMap, List<PTable> toDelete, bool hasMergedAny, bool canMergeAny) {
			MergedMap = mergedMap;
			ToDelete = toDelete;
			HasMergedAny = hasMergedAny;
			CanMergeAny = canMergeAny;
		}
	}
}
