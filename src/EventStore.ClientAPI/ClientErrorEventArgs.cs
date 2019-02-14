using System;

namespace EventStore.ClientAPI {
	/// <summary>
	/// Event Arguments for the event raised when an error occurs on an <see cref="IEventStoreConnection"/>.
	/// </summary>
	public class ClientErrorEventArgs : EventArgs {
		/// <summary>
		/// The thrown exception, if one was raised.
		/// </summary>
		public Exception Exception { get; private set; }

		/// <summary>
		/// The <see cref="IEventStoreConnection"/> responsible for raising the event.
		/// </summary>
		public IEventStoreConnection Connection { get; private set; }

		/// <summary>
		/// Constructs a new instance of <see cref="ClientErrorEventArgs"/>.
		/// </summary>
		/// <param name="connection">The <see cref="IEventStoreConnection"/> responsible for raising the event.</param>
		/// <param name="exception">The thrown exception, if one was raised.</param>
		public ClientErrorEventArgs(IEventStoreConnection connection, Exception exception) {
			Connection = connection;
			Exception = exception;
		}
	}
}
