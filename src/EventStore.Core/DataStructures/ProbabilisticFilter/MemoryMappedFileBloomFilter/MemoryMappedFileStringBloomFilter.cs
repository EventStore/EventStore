using System;
using System.Runtime.InteropServices;

namespace EventStore.Core.DataStructures.ProbabilisticFilter.MemoryMappedFileBloomFilter {
	public class MemoryMappedFileStringBloomFilter : MemoryMappedFileBloomFilter<string> {
		public MemoryMappedFileStringBloomFilter(string path, long size) : base(path, size)
		{
		}

		protected override ReadOnlySpan<byte> Serialize(string item) => MemoryMarshal.AsBytes(item.AsSpan());
	}
}
