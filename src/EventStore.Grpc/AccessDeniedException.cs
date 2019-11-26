using System;

namespace EventStore.Grpc {
	/// <summary>
	/// Exception thrown when a user is not authorised to carry out
	/// an operation.
	/// </summary>
	public class AccessDeniedException : Exception {
		/// <summary>
		/// Constructs a new <see cref="AccessDeniedException" />.
		/// </summary>
		public AccessDeniedException(string message, Exception innerException) : base(message, innerException) {
		}
	}
}
