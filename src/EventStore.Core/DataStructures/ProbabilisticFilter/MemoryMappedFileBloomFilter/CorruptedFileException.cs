using System;

namespace EventStore.Core.DataStructures.ProbabilisticFilter.MemoryMappedFileBloomFilter {
	public class CorruptedFileException : Exception {
		public CorruptedFileException(string error) : base(error) { }
	}
}
