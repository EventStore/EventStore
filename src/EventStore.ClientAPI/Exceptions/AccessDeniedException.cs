using System;
using System.Runtime.Serialization;

namespace EventStore.ClientAPI.Exceptions {
	/// <summary>
	/// Exception thrown when a user is not authorised to carry out
	/// an operation.
	/// </summary>
	public class AccessDeniedException : EventStoreConnectionException {
		/// <summary>
		/// Constructs a new <see cref="AccessDeniedException" />.
		/// </summary>
		public AccessDeniedException() : base("Access denied") {
		}

		/// <summary>
		/// Constructs a new <see cref="AccessDeniedException" />.
		/// </summary>
		public AccessDeniedException(string message) : base(message) {
		}

		/// <summary>
		/// Constructs a new <see cref="AccessDeniedException" />.
		/// </summary>
		public AccessDeniedException(string message, Exception innerException) : base(message, innerException) {
		}

		/// <summary>
		/// Constructs a new <see cref="AccessDeniedException" />.
		/// </summary>
		protected AccessDeniedException(SerializationInfo info, StreamingContext context) : base(info, context) {
		}
	}
}
