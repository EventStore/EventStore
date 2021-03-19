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
		/// The ssl endpoint of the node.
		/// </summary>
		public readonly IPEndPoint SecureTcpEndPoint;

		/// <summary>
		/// The target host for this node.
		/// </summary>
		public readonly string TargetHost;


		/// <summary>
		/// Called to create a new NodeEndPoints
		/// </summary>
		/// <param name="tcpEndPoint">The tcp endpoint of the node</param>
		/// <param name="secureTcpEndPoint">The ssl endpoint of the node</param>
		public NodeEndPoints(IPEndPoint tcpEndPoint, IPEndPoint secureTcpEndPoint, string targetHost) {
			if ((tcpEndPoint ?? secureTcpEndPoint) == null) throw new ArgumentException("Both endpoints are null.");
			if (targetHost == null) throw new ArgumentException(nameof(targetHost));
			TcpEndPoint = tcpEndPoint;
			SecureTcpEndPoint = secureTcpEndPoint;
			TargetHost = targetHost;
		}

		/// <summary>
		/// Formats the endpoints as a string
		/// </summary>
		public override string ToString() {
			return string.Format("[{0}, {1}, ({2})]",
				TcpEndPoint == null ? "n/a" : TcpEndPoint.ToString(),
				SecureTcpEndPoint == null ? "n/a" : SecureTcpEndPoint.ToString(),
				TargetHost);
		}
	}
}
