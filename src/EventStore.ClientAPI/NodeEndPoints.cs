using System;
using System.Net;

namespace EventStore.ClientAPI {
	/// <summary>
	/// Represents a node and its possible endpoints
	/// </summary>
	public struct NodeEndPoints {
		/// <summary>
		/// The tcp endpoint of the node.
		/// </summary>
		public readonly IPEndPoint TcpEndPoint;

		/// <summary>
		/// The ssl endpoint of the node
		/// </summary>
		public readonly IPEndPoint SecureTcpEndPoint;

		/// <summary>
		/// The Host of the endpoints if any.
		/// </summary>
		public readonly string Host;


		/// <summary>
		/// Called to create a new NodeEndPoints
		/// </summary>
		/// <param name="tcpEndPoint">The tcp endpoint of the node</param>
		/// <param name="secureTcpEndPoint">The ssl endpoint of the node</param>
		/// <param name="host">The host of the endpoints if any</param>
		public NodeEndPoints(IPEndPoint tcpEndPoint, IPEndPoint secureTcpEndPoint, string host = null) {
			if ((tcpEndPoint ?? secureTcpEndPoint) == null) throw new ArgumentException("Both endpoints are null.");
			TcpEndPoint = tcpEndPoint;
			SecureTcpEndPoint = secureTcpEndPoint;
			Host = host;
		}

		/// <summary>
		/// Formats the endpoints as a string
		/// </summary>
		public override string ToString() {
			return string.Format("[{0}, {1}]",
				TcpEndPoint == null ? "n/a" : TcpEndPoint.ToString(),
				SecureTcpEndPoint == null ? "n/a" : SecureTcpEndPoint.ToString());
		}
	}
}
