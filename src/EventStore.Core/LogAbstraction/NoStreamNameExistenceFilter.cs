namespace EventStore.Core.LogAbstraction {
	public class NoStreamNameExistenceFilter<TValue> : INameExistenceFilter<TValue> {
		public void InitializeWithExisting(INameLookup<TValue> source) { }
		public void Add(string name, TValue value) { }
		public bool? Exists(string name) => null;
		public void Dispose() { }
	}
}
