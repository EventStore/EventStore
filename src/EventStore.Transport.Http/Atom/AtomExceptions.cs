using System;
using System.Runtime.Serialization;

namespace EventStore.Transport.Http.Atom {
	public class AtomSpecificationViolationException : Exception {
		public AtomSpecificationViolationException() {
		}

		public AtomSpecificationViolationException(string message) : base(message) {
		}

		public AtomSpecificationViolationException(string message, Exception innerException) : base(message,
			innerException) {
		}

		protected AtomSpecificationViolationException(SerializationInfo info, StreamingContext context) : base(info,
			context) {
		}
	}

	public class ThrowHelper {
		public static void ThrowSpecificationViolation(string message) {
			throw new AtomSpecificationViolationException(message);
		}

		public static void ThrowSpecificationViolation(string message, Exception innnerException) {
			throw new AtomSpecificationViolationException(message, innnerException);
		}
	}
}
