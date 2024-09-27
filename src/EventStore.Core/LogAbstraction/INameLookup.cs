namespace EventStore.Core.LogAbstraction {
	/// Looks up a name given a value
	public interface INameLookup<TValue> {
		/// returns false if there is no max (i.e. the source is empty)
		bool TryGetLastValue(out TValue last);

		bool TryGetName(TValue value, out string name);

		string LookupName(TValue value) {
			TryGetName(value, out var name);
			return name;
		}
	}
}
