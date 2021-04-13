namespace EventStore.Core.LogAbstraction {
	public interface ISizer<T> {
		int GetSizeInBytes(T t);
	}
}
