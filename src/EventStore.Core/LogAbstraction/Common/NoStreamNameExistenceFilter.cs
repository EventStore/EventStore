using System;

namespace EventStore.Core.LogAbstraction.Common {
	public class NoStreamNameExistenceFilter : INameExistenceFilter {
		public void Initialize(INameEnumerator source) { }
		public long CurrentCheckpoint => throw new NotImplementedException(); //qq

		public void Add(string name, long value) { }
		public void Add(ulong hash, long checkpoint) { }
		public bool MightExist(string name) => true;
		public void Dispose() { }
	}
}
