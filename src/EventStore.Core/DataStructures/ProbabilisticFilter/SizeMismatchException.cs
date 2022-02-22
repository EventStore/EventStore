using System;

namespace EventStore.Core.DataStructures.ProbabilisticFilter {
	public class SizeMismatchException : Exception {
		public SizeMismatchException(string error) : base(error) { }
	}
}
