using System;

namespace EventStore.BufferManagement {
	public class UnableToAllocateBufferException : Exception {
		public UnableToAllocateBufferException()
			: base("Cannot allocate buffer after few trials.") {
		}
	}
}
