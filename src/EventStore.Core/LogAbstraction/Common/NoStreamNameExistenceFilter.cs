using System;

namespace EventStore.Core.LogAbstraction.Common {
	public class NoStreamNameExistenceFilter : INameExistenceFilter {
		public void Initialize(INameExistenceFilterInitializer source) { }
		public long CurrentCheckpoint => throw new NotImplementedException(); //qq

		public void Add(string name, long value) { }
		public void Add(ulong hash, long checkpoint) { }
		public bool MightContain(string name) => true;
		public void Dispose() { }
	}
}
