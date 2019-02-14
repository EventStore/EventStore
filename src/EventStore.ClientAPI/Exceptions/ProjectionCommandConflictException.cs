using System;
using System.Runtime.Serialization;

namespace EventStore.ClientAPI.Exceptions {
	/// <summary>
	/// Exception thrown if a projection command fails.
	/// </summary>
	public class ProjectionCommandConflictException : ProjectionCommandFailedException {
		/// <summary>
		/// Constructs a new <see cref="ProjectionCommandFailedException"/>.
		/// </summary>
		public ProjectionCommandConflictException() {
		}

		/// <summary>
		/// Constructs a new <see cref="ProjectionCommandFailedException"/>.
		/// </summary>
		public ProjectionCommandConflictException(int httpStatusCode, string message)
			: base(httpStatusCode, message) {
		}

		/// <summary>
		/// Constructs a new <see cref="ProjectionCommandFailedException"/>.
		/// </summary>
		public ProjectionCommandConflictException(string message,
			Exception innerException)
			: base(message, innerException) {
		}

		/// <summary>
		/// Constructs a new <see cref="ProjectionCommandFailedException"/>.
		/// </summary>
		protected ProjectionCommandConflictException(SerializationInfo info,
			StreamingContext context)
			: base(info, context) {
		}
	}
}
