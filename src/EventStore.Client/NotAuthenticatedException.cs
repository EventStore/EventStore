using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace EventStore.Client {
	public class NotAuthenticatedException : Exception {
		public NotAuthenticatedException() {
		}

		public NotAuthenticatedException(string message) : base(message) {
		}

		public NotAuthenticatedException(string message, Exception innerException) : base(message, innerException) {
		}

		protected NotAuthenticatedException(SerializationInfo info, StreamingContext context) : base(info, context) {
		}
	}
}
