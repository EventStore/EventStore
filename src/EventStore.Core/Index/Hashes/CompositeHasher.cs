namespace EventStore.Core.Index.Hashes {
	public class CompositeHasher<T> : ILongHasher<T> {
		private readonly IHasher<T> _lowHasher;
		private readonly IHasher<T> _highHasher;

		public CompositeHasher(IHasher<T> lowHasher, IHasher<T> highHasher) {
			_lowHasher = lowHasher;
			_highHasher = highHasher;
		}

		public ulong Hash(T x) {
			// same way around as Tableindex for consistency.
			return (ulong)_lowHasher.Hash(x) << 32 | _highHasher.Hash(x);
		}
	}
}
