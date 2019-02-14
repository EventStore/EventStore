using System;

namespace EventStore.Core.Exceptions {
	public class ExtraneousFileFoundException : Exception {
		public ExtraneousFileFoundException(string message) : base(message) {
		}
	}
}
