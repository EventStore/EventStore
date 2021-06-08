using System;
using System.Runtime.InteropServices;

namespace EventStore.Core.DataStructures.ProbabilisticFilter.MemoryMappedFileBloomFilter {
	public class MemoryMappedFileStreamBloomFilter : MemoryMappedFileBloomFilter {
		public MemoryMappedFileStreamBloomFilter(string path, long size, int initialReaderCount, int maxReaderCount) :
			base(path, size, initialReaderCount, maxReaderCount)
		{
		}

		public void Add(string stream) =>
			Add(Serialize(stream));

		public void Add(ulong streamHash) =>
			Add(Serialize(streamHash));

		public bool MayExist(string stream) =>
			MayExist(Serialize(stream));

		public bool MayExist(ulong streamHash) =>
			MayExist(Serialize(streamHash));

		//qq probably the standard hash function is what we want to handle here (for v2) (( but not for v3))

		private static ReadOnlySpan<byte> Serialize(string stream) => MemoryMarshal.AsBytes(stream.AsSpan());
		private static ReadOnlySpan<byte> Serialize(ulong streamHash) => MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref streamHash, 1));
	}
}
