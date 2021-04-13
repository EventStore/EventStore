namespace EventStore.Core.LogAbstraction {
	public interface IStreamNameLookupFactory<T> {
		IStreamNameLookup<T> Create(object input = null);
	}
}
