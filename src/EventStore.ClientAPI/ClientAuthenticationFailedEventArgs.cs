using System;

namespace EventStore.ClientAPI {
	/// <summary>
	/// Event Arguments for the event raised when an <see cref="IEventStoreConnection"/> fails
	/// to authenticate against an Event Store server.
	/// </summary>
	public class ClientAuthenticationFailedEventArgs : EventArgs {
		/// <summary>
		/// A reason for authentication failure, if known.
		/// </summary>
		public string Reason { get; private set; }

		/// <summary>
		/// The <see cref="IEventStoreConnection"/> responsible for raising the event.
		/// </summary>
		public IEventStoreConnection Connection { get; private set; }

		/// <summary>
		/// Constructs a new instance of <see cref="ClientAuthenticationFailedEventArgs"/>.
		/// </summary>
		/// <param name="connection">The <see cref="IEventStoreConnection"/> responsible for raising the event.</param>
		/// <param name="reason">A reason for authentication failure, if known.</param>
		public ClientAuthenticationFailedEventArgs(IEventStoreConnection connection, string reason) {
			Connection = connection;
			Reason = reason;
		}
	}
}
