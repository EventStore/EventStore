using System;
using System.Net;

namespace EventStore.ClientAPI {
	/// <summary>
	/// Represents a source of cluster gossip.
	/// </summary>
	public class GossipSeed {
		/// <summary>
		/// The <see cref="EndPoint"/> for the HTTP endpoint of the gossip seed.
		///
		/// The HTTP endpoint is used rather than the TCP endpoint because it is required
		/// for the client to exchange gossip with the server. The standard port which should be
		/// used here is 2113.
		/// </summary>
		public readonly EndPoint EndPoint;

		/// <summary>
		/// If Gossip should be requested over Tls
		/// </summary>
		public readonly bool SeedOverTls;

		/// <summary>
		/// If Gossip should be requested with no "Host" header to be compatible with v5 clusters.
		/// </summary>
		public readonly bool V5HostHeader;

		/// <summary>
		/// Creates a new <see cref="GossipSeed" />.
		/// </summary>
		/// <param name="endPoint">The <see cref="EndPoint"/> for the HTTP endpoint of the gossip seed. The standard port is 2113.</param>
		/// <param name="seedOverTls">Specifies that eventstore should use https when connecting to gossip</param>
		/// <param name="v5HostHeader">Specifies that eventstore should request gossip with no "Host" header to be compatible with v5 clusters.</param>
		public GossipSeed(EndPoint endPoint, bool seedOverTls = true, bool v5HostHeader = false) {
			EndPoint = endPoint;
			SeedOverTls = seedOverTls;
			V5HostHeader = v5HostHeader;
		}
	}
}
