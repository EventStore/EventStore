namespace EventStore.Core.Index.Hashes {
	public class CompositeHasher<T> : ILongHasher<T> {
		private readonly IHasher<T> _lowHasher;
		private readonly IHasher<T> _highHasher;

		public CompositeHasher(IHasher<T> lowHasher, IHasher<T> highHasher) {
			_lowHasher = lowHasher;
			_highHasher = highHasher;
		}

		public ulong Hash(T x) {
			//qq this is the wrong way round, but so is the one in tableindex
			return (ulong)_lowHasher.Hash(x) << 32 | _highHasher.Hash(x);
		}
	}
}
