using System;

namespace EventStore.Core.Index.Hashes {
	public interface IHasher {
		uint Hash(byte[] data);
		uint Hash(byte[] data, int offset, uint len, uint seed);
		uint Hash(ReadOnlySpan<byte> data);
	}

	public interface IHasher<TStreamId> {
		uint Hash(TStreamId s);
	}

	public interface ILongHasher<T> {
		ulong Hash(T x);
	}
}
