using System;
using System.Runtime.Serialization;

namespace EventStore.ClientAPI.Exceptions {
	/// <summary>
	/// Exception thrown if an operation requires authentication but
	/// the client is not authenticated.
	/// </summary>
	public class NotAuthenticatedException : EventStoreConnectionException {
		/// <summary>
		/// Constructs a new <see cref="NotAuthenticatedException"/>.
		/// </summary>
		public NotAuthenticatedException() : base("Authentication error") {
		}

		/// <summary>
		/// Constructs a new <see cref="NotAuthenticatedException"/>.
		/// </summary>
		public NotAuthenticatedException(string message) : base(message) {
		}

		/// <summary>
		/// Constructs a new <see cref="NotAuthenticatedException"/>.
		/// </summary>
		public NotAuthenticatedException(string message, Exception innerException) : base(message, innerException) {
		}

		/// <summary>
		/// Constructs a new <see cref="NotAuthenticatedException"/>.
		/// </summary>
		protected NotAuthenticatedException(SerializationInfo info, StreamingContext context) : base(info, context) {
		}
	}
}
