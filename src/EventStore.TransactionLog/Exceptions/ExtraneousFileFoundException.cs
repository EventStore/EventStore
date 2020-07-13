using System;

namespace EventStore.Core.TransactionLog.Exceptions {
	public class ExtraneousFileFoundException : Exception {
		public ExtraneousFileFoundException(string message) : base(message) {
		}
	}
}
