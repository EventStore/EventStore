using System.Collections.Generic;
using EventStore.Core.Index.Hashes;
using EventStore.Core.LogAbstraction;

namespace EventStore.Core.XUnit.Tests.LogV2 {
	public class MockExistenceFilter : INameExistenceFilter {
		private readonly ILongHasher<string> _hasher;

		public MockExistenceFilter(ILongHasher<string> hasher) {
			_hasher = hasher;
		}
		public HashSet<ulong> Hashes { get; } = new();

		public long CurrentCheckpoint { get; set; } = -1;

		public void Add(string name) {
			Hashes.Add(_hasher.Hash(name));
		}

		public void Add(ulong hash) {
			Hashes.Add(hash);
		}

		public void Dispose() {
		}

		public void Initialize(INameExistenceFilterInitializer source) {
			source.Initialize(this);
		}

		public bool MightContain(string name) {
			return Hashes.Contains(_hasher.Hash(name));
		}
	}
}
