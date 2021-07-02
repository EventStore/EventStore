using System;
using System.Runtime.InteropServices;
using EventStore.Common.Utils;
using EventStore.Core.Index.Hashes;

namespace EventStore.Core.DataStructures.ProbabilisticFilter.MemoryMappedFileBloomFilter {
	// this class exists to deal with the fact that v3 stores strings in the bloom filter
	// but v2 stores hashes (to make it faster to rebuild from the standard index)
	//
	// if you provide a ILongHasher then strings are hashed using it and the resulting ulong
	// added to the filter. if you add a ulong directly, it must be the result of the same hash.
	public class MemoryMappedFileStreamBloomFilter : MemoryMappedFileBloomFilter {
		private readonly ILongHasher<string> _hasher;

		public MemoryMappedFileStreamBloomFilter(string path, long size, int initialReaderCount, int maxReaderCount,
			ILongHasher<string> hasher) :
			base(path, size, initialReaderCount, maxReaderCount) {
			_hasher = hasher;
		}

		public void Add(string stream) =>
			Add(SerializeString(stream));

		public void Add(ulong streamHash) {
			Ensure.NotNull(_hasher, "Hasher");
			Add(SerializeHash(streamHash));
		}

		public bool MightContain(string stream) =>
			MightContain(SerializeString(stream));

		private ReadOnlySpan<byte> SerializeString(string stream) {
			if (_hasher != null)
				return SerializeHash(_hasher.Hash(stream));

			return MemoryMarshal.AsBytes(stream.AsSpan());
		}

		private static ReadOnlySpan<byte> SerializeHash(ulong streamHash) =>
			MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref streamHash, 1));
	}
}
