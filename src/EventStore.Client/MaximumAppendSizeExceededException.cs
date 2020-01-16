using System;

namespace EventStore.Client {
	/// <summary>
	/// Exception thrown when an append exceeds the maximum size set by the server.
	/// </summary>
	public class MaximumAppendSizeExceededException : Exception {
		public MaximumAppendSizeExceededException(int maxAppendSize, Exception innerException) :
			base($"Maximum Append Size of {maxAppendSize} Exceeded.", innerException) {
		}
	}
}
