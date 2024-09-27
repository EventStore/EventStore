using System.Collections.Generic;
using System.Threading;
using EventStore.Core.Index.Hashes;
using EventStore.Core.LogAbstraction;

namespace EventStore.Core.XUnit.Tests.LogV2 {
	public class MockExistenceFilter : INameExistenceFilter {
		private readonly ILongHasher<string> _hasher;
		private readonly int _addDelayMs;

		public MockExistenceFilter(ILongHasher<string> hasher, int addDelayMs = 0) {
			_hasher = hasher;
			_addDelayMs = addDelayMs;
		}

		public HashSet<ulong> Hashes { get; } = new();

		public long CurrentCheckpoint { get; set; } = -1;

		public void Add(string name) {
			if (_addDelayMs > 0)
				Thread.Sleep(_addDelayMs);
			Hashes.Add(_hasher.Hash(name));
		}

		public void Add(ulong hash) {
			if (_addDelayMs > 0)
				Thread.Sleep(_addDelayMs);
			Hashes.Add(hash);
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

		public bool MightContain(string name) {
			return Hashes.Contains(_hasher.Hash(name));
		}
	}
}
