using System;

namespace EventStore.Core.TransactionLogV2.Exceptions {
	public class ExtraneousFileFoundException : Exception {
		public ExtraneousFileFoundException(string message) : base(message) {
		}
	}
}
