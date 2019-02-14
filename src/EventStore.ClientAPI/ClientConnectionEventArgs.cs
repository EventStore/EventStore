using System;
using System.Net;

namespace EventStore.ClientAPI {
	/// <summary>
	/// Event Arguments for the event raised when an <see cref="IEventStoreConnection"/> is
	/// connected to or disconnected from an Event Store server.
	/// </summary>
	public class ClientConnectionEventArgs : EventArgs {
		/// <summary>
		/// The endpoint of the Event Store server to or from which the connection was connected or disconnected.
		/// </summary>
		public IPEndPoint RemoteEndPoint { get; private set; }

		/// <summary>
		/// The <see cref="IEventStoreConnection"/> responsible for raising the event.
		/// </summary>
		public IEventStoreConnection Connection { get; private set; }

		/// <summary>
		/// Constructs a new instance of <see cref="ClientConnectionEventArgs"/>.
		/// </summary>
		/// <param name="connection">The <see cref="IEventStoreConnection"/> responsible for raising the event.</param>
		/// <param name="remoteEndPoint">The endpoint of the Event Store server to or from which the connection was connected or disconnected.</param>
		public ClientConnectionEventArgs(IEventStoreConnection connection, IPEndPoint remoteEndPoint) {
			Connection = connection;
			RemoteEndPoint = remoteEndPoint;
		}
	}
}
