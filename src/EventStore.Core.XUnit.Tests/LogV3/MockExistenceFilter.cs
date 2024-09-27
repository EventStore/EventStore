using System.Collections.Generic;
using EventStore.Core.LogAbstraction;

namespace EventStore.Core.XUnit.Tests.LogV3 {
	public class MockExistenceFilter : INameExistenceFilter {
		public HashSet<string> Streams { get; } = new();

		public long CurrentCheckpoint { get; set; } = -1;

		public void Add(string name) {
			Streams.Add(name);
		}

		public void Add(ulong hash) {
			throw new System.NotImplementedException();
		}

		public void Dispose() {
		}

		public void Initialize(INameExistenceFilterInitializer source, long truncateToPosition) {
			source.Initialize(this, truncateToPosition);
		}

		public void TruncateTo(long checkpoint) {
			CurrentCheckpoint = checkpoint;
		}

		public void Verify(double corruptionThreshold) { }

		public bool MightContain(string item) {
			return Streams.Contains(item);
		}
	}
}
