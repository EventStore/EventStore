using System;

namespace EventStore.Client {
	public class DiscoveryException : Exception {
		public DiscoveryException(string message)
			: base(message) {
		}

		public DiscoveryException(string message, Exception innerException)
			: base(message, innerException) {
		}
	}
}
