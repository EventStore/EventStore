using System;

namespace EventStore.Core.DataStructures.ProbabilisticFilter {
	public class CorruptedFileException : Exception {
		public CorruptedFileException(string error, Exception inner = null) : base(error, inner) { }
	}
}
