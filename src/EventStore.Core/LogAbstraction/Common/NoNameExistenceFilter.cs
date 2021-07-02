using System;

namespace EventStore.Core.LogAbstraction.Common {
	public class NoNameExistenceFilter : INameExistenceFilter {
		public void Initialize(INameExistenceFilterInitializer source) { }
		public long CurrentCheckpoint => throw new NotSupportedException();

		public void Add(string name, long value) { }
		public void Add(ulong hash, long checkpoint) { }
		public bool MightContain(string name) => true;
		public void Dispose() { }
	}
}
