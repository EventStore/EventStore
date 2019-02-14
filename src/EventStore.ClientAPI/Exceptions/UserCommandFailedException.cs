using System;
using System.Runtime.Serialization;

namespace EventStore.ClientAPI.Exceptions {
	/// <summary>
	/// Exception thrown if a projection command fails.
	/// </summary>
	public class UserCommandFailedException : EventStoreConnectionException {
		/// <summary>
		/// The Http status code returned for the operation
		/// </summary>
		public int HttpStatusCode { get; private set; }

		/// <summary>
		/// Constructs a new <see cref="UserCommandFailedException"/>.
		/// </summary>
		public UserCommandFailedException() {
		}

		/// <summary>
		/// Constructs a new <see cref="UserCommandFailedException"/>.
		/// </summary>
		public UserCommandFailedException(int httpStatusCode, string message) : base(message) {
			HttpStatusCode = httpStatusCode;
		}

		/// <summary>
		/// Constructs a new <see cref="UserCommandFailedException"/>.
		/// </summary>
		public UserCommandFailedException(string message,
			Exception innerException) : base(message, innerException) {
		}

		/// <summary>
		/// Constructs a new <see cref="UserCommandFailedException"/>.
		/// </summary>
		protected UserCommandFailedException(SerializationInfo info,
			StreamingContext context) : base(info, context) {
		}
	}
}
