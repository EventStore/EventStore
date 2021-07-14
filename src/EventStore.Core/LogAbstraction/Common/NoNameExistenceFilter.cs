using System;

namespace EventStore.Core.LogAbstraction.Common {
	public class NoNameExistenceFilter : INameExistenceFilter {
		public void Initialize(INameExistenceFilterInitializer source) { }
		public long CurrentCheckpoint {
			get => throw new NotSupportedException();
			set { }
		}

		public void Add(string name) { }
		public void Add(ulong hash) { }
		public bool MightContain(string name) => true;
		public void Dispose() { }
	}
}
