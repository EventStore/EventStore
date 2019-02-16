using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EventStore.Common.Exceptions {
	public class ApplicationInitializationException : Exception {
		public ApplicationInitializationException(string message) : base(message) {
		}

		public ApplicationInitializationException(string message, Exception innerException) : base(message,
			innerException) {
		}
	}
}
