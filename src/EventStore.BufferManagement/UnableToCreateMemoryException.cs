using System;

namespace EventStore.BufferManagement {
	public class UnableToCreateMemoryException : Exception {
		public UnableToCreateMemoryException()
			: base("All buffers were in use and acquiring more memory has been disabled.") {
		}
	}
}
