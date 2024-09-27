using System;

namespace EventStore.Common.Exceptions {
	public class InvalidConfigurationException : Exception {
		public InvalidConfigurationException(string message) : base(message) { }
	}
}
