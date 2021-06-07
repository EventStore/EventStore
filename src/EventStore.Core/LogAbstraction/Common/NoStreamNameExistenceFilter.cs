namespace EventStore.Core.LogAbstraction.Common {
	public class NoStreamNameExistenceFilter : INameExistenceFilter {
		public void Initialize(INameEnumerator source) { }
		public void Add(string name, long value) { }
		public bool MightExist(string name) => true;
		public void Dispose() { }
	}
}
