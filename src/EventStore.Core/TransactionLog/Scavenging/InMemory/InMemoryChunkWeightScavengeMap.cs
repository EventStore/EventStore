namespace EventStore.Core.TransactionLog.Scavenging {
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
			// sqlite implementaiton would update table set weight = weight + extraWeight
			if (!TryGetValue(logicalChunkNumber, out var weight))
				weight = 0;
			this[logicalChunkNumber] = weight + extraWeight;
		}

		public void ResetChunkWeights(int startLogicalChunkNumber, int endLogicalChunkNumber) {
			// sqlite implementation would delete * from table where chunkNumber in range

			for (var i = startLogicalChunkNumber; i <= endLogicalChunkNumber; i++) {
				TryRemove(i, out _);
			}
		}

		public float SumChunkWeights(int startLogicalChunkNumber, int endLogicalChunkNumber) {
			// sqlite implementation would select sum weight from table where chunkNumber in range

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
