namespace EventStore.Core.TransactionLog.Scavenging {
	public interface IChunkWeightScavengeMap : IScavengeMap<int, float> {
		bool AllWeightsAreZero();
		void IncreaseWeight(int logicalChunkNumber, float extraWeight);
		float SumChunkWeights(int startLogicalChunkNumber, int endLogicalChunkNumber);
		void ResetChunkWeights(int startLogicalChunkNumber, int endLogicalChunkNumber);
	}
}
