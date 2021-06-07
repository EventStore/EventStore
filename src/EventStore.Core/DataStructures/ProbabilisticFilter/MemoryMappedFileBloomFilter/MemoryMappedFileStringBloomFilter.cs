using System;
using System.Runtime.InteropServices;

namespace EventStore.Core.DataStructures.ProbabilisticFilter.MemoryMappedFileBloomFilter {
	public class MemoryMappedFileStringBloomFilter : MemoryMappedFileBloomFilter<string> {
		public MemoryMappedFileStringBloomFilter(string path, long size, int initialReaderCount, int maxReaderCount) :
			base(path, size, initialReaderCount, maxReaderCount)
		{
		}

		protected override ReadOnlySpan<byte> Serialize(string item) => MemoryMarshal.AsBytes(item.AsSpan());
	}
}
