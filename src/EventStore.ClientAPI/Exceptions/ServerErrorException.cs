using System;
using System.Runtime.Serialization;

namespace EventStore.ClientAPI.Exceptions {
	/// <summary>
	/// Exception thrown if a server-side error occurs during an
	/// operation.
	/// </summary>
	public class ServerErrorException : EventStoreConnectionException {
		/// <summary>
		/// Constructs a new instance of <see cref="ServerErrorException"/>.
		/// </summary>
		public ServerErrorException() {
		}

		/// <summary>
		/// Constructs a new instance of <see cref="ServerErrorException"/>.
		/// </summary>
		public ServerErrorException(string message)
			: base(string.Format("Unexpected error on server: {0}", message)) {
		}

		/// <summary>
		/// Constructs a new instance of <see cref="ServerErrorException"/>.
		/// </summary>
		public ServerErrorException(string message, Exception innerException)
			: base(string.Format("Unexpected error on server: {0}", message), innerException) {
		}

		/// <summary>
		/// Constructs a new instance of <see cref="ServerErrorException"/>.
		/// </summary>
		protected ServerErrorException(SerializationInfo info, StreamingContext context) : base(info, context) {
		}
	}
}
