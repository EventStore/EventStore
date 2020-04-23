using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventStore.ClientAPI.Exceptions {
	/// <summary>
	/// Exception thrown if an operation is not supported by a node.
	/// For example: Write operations are not supported by read only nodes.
	/// </summary>
	public class OperationNotSupportedException : EventStoreConnectionException {
		/// <summary>
		/// Constructs a new instance of <see cref="OperationNotSupportedException"/>.
		/// </summary>
		/// <param name="operation">The name of the operation attempted.</param>
		/// <param name="reason">The reason the operation is not supported.</param>
		public OperationNotSupportedException(string operation, string reason)
			: base(string.Format("Operation '{0}' is not supported : {1}", operation, reason)) {
		}

		/// <summary>
		/// Constructs a new instance of <see cref="OperationNotSupportedException"/>.
		/// </summary>
		/// <param name="operation">The name of the operation attempted.</param>
		public OperationNotSupportedException(string operation)
			: base(string.Format("Operation '{0}' is not supported", operation)) {
		}
	}
}
