using System;

namespace EventStore.Core.DataStructures.ProbabilisticFilter {
	public class CorruptedHashException : Exception {
		public CorruptedHashException(int rebuildCount, string error) : base(error) {
			RebuildCount = rebuildCount;
		}

		public int RebuildCount { get; }
	}
}
