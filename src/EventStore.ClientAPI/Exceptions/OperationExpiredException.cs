using System;
using System.Runtime.Serialization;

namespace EventStore.ClientAPI.Exceptions {
	/// <summary>
	/// Exception thrown if an operation expires before it can be scheduled.
	/// </summary>
	public class OperationExpiredException : EventStoreConnectionException {
		/// <summary>
		/// Constructs a new <see cref="OperationExpiredException"/>.
		/// </summary>
		public OperationExpiredException() {
		}

		/// <summary>
		/// Constructs a new <see cref="OperationExpiredException"/>.
		/// </summary>
		public OperationExpiredException(string message) : base(message) {
		}

		/// <summary>
		/// Constructs a new <see cref="OperationExpiredException"/>.
		/// </summary>
		public OperationExpiredException(string message, Exception innerException) : base(message, innerException) {
		}

		/// <summary>
		/// Constructs a new <see cref="OperationExpiredException"/>.
		/// </summary>
		protected OperationExpiredException(SerializationInfo info, StreamingContext context) : base(info, context) {
		}
	}
}
