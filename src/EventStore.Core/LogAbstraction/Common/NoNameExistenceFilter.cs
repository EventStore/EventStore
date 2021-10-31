namespace EventStore.Core.LogAbstraction.Common {
	public class NoNameExistenceFilter : INameExistenceFilter {
		public void Initialize(INameExistenceFilterInitializer source, long truncateToPosition) { }
		public void TruncateTo(long checkpoint) { }
		public void Verify(double corruptionThreshold) { }
		public long CurrentCheckpoint { get; set; } = -1;

		public void Add(string name) { }
		public void Add(ulong hash) { }
		public bool MightContain(string name) => true;
		public void Dispose() { }
	}
}
