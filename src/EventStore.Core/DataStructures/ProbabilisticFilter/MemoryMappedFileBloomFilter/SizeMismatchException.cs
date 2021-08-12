using System;

namespace EventStore.Core.DataStructures.ProbabilisticFilter.MemoryMappedFileBloomFilter {
	public class SizeMismatchException : Exception {
		public SizeMismatchException(string error) : base(error) { }
	}
}
