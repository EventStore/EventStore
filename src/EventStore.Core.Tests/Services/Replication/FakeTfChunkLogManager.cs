using System;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.Chunks;

namespace EventStore.Core.Tests.Services.Replication {
	public class FakeTfChunkLogManager : ITFChunkScavengerLogManager {
		public void Initialise() {
		}

		public ITFChunkScavengerLog CreateLog() {
			return new FakeTFScavengerLog();
		}
	}

	public class FakeTfChunkScavengerLog : ITFChunkScavengerLog {
		public void IndexTableScavenged(int level, int index, TimeSpan elapsed, long entriesDeleted, long entriesKept,
			long spaceSaved) {
		}

		public void IndexTableNotScavenged(int level, int index, TimeSpan elapsed, long entriesKept,
			string errorMessage) {
		}

		public string ScavengeId { get; }
		public long SpaceSaved { get; }

		public void ScavengeStarted() {
		}

		public void ChunksScavenged(int chunkStartNumber, int chunkEndNumber, TimeSpan elapsed, long spaceSaved) {
		}

		public void ChunksNotScavenged(int chunkStartNumber, int chunkEndNumber, TimeSpan elapsed,
			string errorMessage) {
		}

		public void ChunksMerged(int chunkStartNumber, int chunkEndNumber, TimeSpan elapsed, long spaceSaved) {
		}

		public void ChunksNotMerged(int chunkStartNumber, int chunkEndNumber, TimeSpan elapsed, string errorMessage) {
		}

		public void ScavengeCompleted(ScavengeResult result, string error, TimeSpan elapsed) {
		}
	}
}
