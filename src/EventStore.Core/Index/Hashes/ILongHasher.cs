namespace EventStore.Core.Index.Hashes {
	public interface ILongHasher<T> {
		ulong Hash(T x);
	}
}
