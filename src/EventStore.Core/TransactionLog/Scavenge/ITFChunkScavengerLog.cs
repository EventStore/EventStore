using System;
using EventStore.Core.Index;

namespace EventStore.Core.TransactionLog.Chunks {
	public interface ITFChunkScavengerLog : IIndexScavengerLog {
		string ScavengeId { get; }

		long SpaceSaved { get; }

		void ScavengeStarted();

		void ChunksScavenged(int chunkStartNumber, int chunkEndNumber, TimeSpan elapsed, long spaceSaved);

		void ChunksNotScavenged(int chunkStartNumber, int chunkEndNumber, TimeSpan elapsed, string errorMessage);

		void ChunksMerged(int chunkStartNumber, int chunkEndNumber, TimeSpan elapsed, long spaceSaved);

		void ChunksNotMerged(int chunkStartNumber, int chunkEndNumber, TimeSpan elapsed, string errorMessage);

		void ScavengeCompleted(ScavengeResult result, string error, TimeSpan elapsed);
	}

	public enum ScavengeResult {
		Success,
		Failed,
		Stopped
	}
}
