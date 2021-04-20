namespace EventStore.Core.Index.Hashes {
	public interface IHasher {
		uint Hash(byte[] data);
		uint Hash(byte[] data, int offset, uint len, uint seed);
	}

	public interface IHasher<TStreamId> : IHasher {
		uint Hash(TStreamId s);
	}
}
