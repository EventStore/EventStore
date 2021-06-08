using System;
using System.Runtime.InteropServices;

namespace EventStore.Core.DataStructures.ProbabilisticFilter.MemoryMappedFileBloomFilter {
	//qq rename
	public class MemoryMappedFileStreamBloomFilter : MemoryMappedFileBloomFilter {
		public MemoryMappedFileStreamBloomFilter(string path, long size, int initialReaderCount, int maxReaderCount) :
			base(path, size, initialReaderCount, maxReaderCount)
		{
		}

		public void Add(string item) =>
			Add(Serialize(item));

		public void Add(ulong hash) =>
			Add(Serialize(hash));

		public bool MayExist(string item) =>
			MayExist(Serialize(item));

		//qq need to use the standard hash functions
		private static ReadOnlySpan<byte> Serialize(string item) => MemoryMarshal.AsBytes(item.AsSpan());

		//qq is this right? seems wordy
		private static ReadOnlySpan<byte> Serialize(ulong hash) => MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref hash, 1));
	}
}
