namespace EventStore.Core.LogAbstraction.Common {
	public class NoStreamNameExistenceFilter<TCheckpoint> : INameExistenceFilter<TCheckpoint> {
		public void Initialize(INameEnumerator<TCheckpoint> source) { }
		public void Add(string name, TCheckpoint value) { }
		public bool? Exists(string name) => null;
		public void Dispose() { }
	}
}
