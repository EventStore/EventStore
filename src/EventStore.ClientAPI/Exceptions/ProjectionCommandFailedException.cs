using System;
using System.Runtime.Serialization;

namespace EventStore.ClientAPI.Exceptions {
	/// <summary>
	/// Exception thrown if a projection command fails.
	/// </summary>
	public class ProjectionCommandFailedException : EventStoreConnectionException {
		/// <summary>
		/// The Http status code returned by the server
		/// </summary>
		public int HttpStatusCode { get; private set; }

		/// <summary>
		/// Constructs a new <see cref="ProjectionCommandFailedException"/>.
		/// </summary>
		public ProjectionCommandFailedException() {
		}

		/// <summary>
		/// Constructs a new <see cref="ProjectionCommandFailedException"/>.
		/// </summary>
		public ProjectionCommandFailedException(int httpStatusCode, string message)
			: base(message) {
			HttpStatusCode = httpStatusCode;
		}

		/// <summary>
		/// Constructs a new <see cref="ProjectionCommandFailedException"/>.
		/// </summary>
		public ProjectionCommandFailedException(string message,
			Exception innerException)
			: base(message, innerException) {
		}

		/// <summary>
		/// Constructs a new <see cref="ProjectionCommandFailedException"/>.
		/// </summary>
		protected ProjectionCommandFailedException(SerializationInfo info,
			StreamingContext context)
			: base(info, context) {
		}
	}
}
