using System;

namespace EventStore.ClientAPI {
	/// <summary>
	/// Event Arguments for the event raised when an <see cref="IEventStoreConnection"/> is
	/// about to reconnect to an Event Store server.
	/// </summary>
	public class ClientReconnectingEventArgs : EventArgs {
		/// <summary>
		/// The <see cref="IEventStoreConnection"/> responsible for raising the event.
		/// </summary>
		public IEventStoreConnection Connection { get; private set; }

		/// <summary>
		/// Constructs a new instance of <see cref="ClientReconnectingEventArgs"/>.
		/// </summary>
		/// <param name="connection">The <see cref="IEventStoreConnection"/> responsible for raising the event.</param>
		public ClientReconnectingEventArgs(IEventStoreConnection connection) {
			Connection = connection;
		}
	}
}
