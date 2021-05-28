namespace EventStore.Core.LogAbstraction {
	/// Looks up a Value given a Name
	public interface IValueLookup<TValue> {
		TValue LookupValue(string name);
	}
}
