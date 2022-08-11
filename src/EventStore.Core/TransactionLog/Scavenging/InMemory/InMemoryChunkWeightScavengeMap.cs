﻿namespace EventStore.Core.TransactionLog.Scavenging {
	public class InMemoryChunkWeightScavengeMap :
		InMemoryScavengeMap<int, float>,
		IChunkWeightScavengeMap {

		public bool AllWeightsAreZero() {
			foreach (var kvp in AllRecords()) {
				if (kvp.Value != 0) {
					return false;
				}
			}
			return true;
		}

		public void IncreaseWeight(int logicalChunkNumber, float extraWeight) {
			if (!TryGetValue(logicalChunkNumber, out var weight))
				weight = 0;
			this[logicalChunkNumber] = weight + extraWeight;
		}

		public void ResetChunkWeights(int startLogicalChunkNumber, int endLogicalChunkNumber) {
			for (var i = startLogicalChunkNumber; i <= endLogicalChunkNumber; i++) {
				TryRemove(i, out _);
			}
		}

		public float SumChunkWeights(int startLogicalChunkNumber, int endLogicalChunkNumber) {
			var totalWeight = 0f;
			for (var i = startLogicalChunkNumber; i <= endLogicalChunkNumber; i++) {
				if (TryGetValue(i, out var weight)) {
					totalWeight += weight;
				}
			}

			return totalWeight;
		}
	}
}
