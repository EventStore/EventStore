using System;

namespace EventStore.Transport.Http.Atom {
	public class AtomSpecificationViolationException : Exception {
		public AtomSpecificationViolationException(string message) : base(message) {
		}
	}

	public class ThrowHelper {
		public static void ThrowSpecificationViolation(string message) {
			throw new AtomSpecificationViolationException(message);
		}
	}
}
