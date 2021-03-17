using System.Net;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Transport.Http;

namespace EventStore.ClientAPI {
	/// <summary>
	/// Represents a source of cluster gossip.
	/// </summary>
	public class GossipSeed: IGossipSeed {
		/// <summary>
		/// The <see cref="IPEndPoint"/> for the External HTTP endpoint of the gossip seed.
		///
		/// The HTTP endpoint is used rather than the TCP endpoint because it is required
		/// for the client to exchange gossip with the server. The standard port which should be
		/// used here is 2113.
		/// </summary>
		public readonly IPEndPoint EndPoint;

		/// <summary>
		/// If Gossip should be requested
		/// </summary>
		public readonly bool SeedOverTls;

		/// <summary>
		/// The host header to be sent when requesting gossip.
		/// </summary>
		public readonly string HostHeader;
		
		private readonly bool _v20Compatibility;

		/// <summary>
		/// Creates a new <see cref="GossipSeed" />.
		/// </summary>
		/// <param name="endPoint">The <see cref="IPEndPoint"/> for the External HTTP endpoint of the gossip seed. The standard port is 2113.</param>
		/// <param name="hostHeader">The host header to be sent when requesting gossip. Defaults to String.Empty</param>
		/// <param name="seedOverTls">Specifies that eventstore should use https when connecting to gossip</param>
		/// <param name="v20Compatibility">If v20 compatibility mode is enabled.</param>
		public GossipSeed(IPEndPoint endPoint, string hostHeader = "", bool seedOverTls = false, bool v20Compatibility = false) {
			EndPoint = endPoint;
			HostHeader = hostHeader == string.Empty ? null : hostHeader;
			SeedOverTls = seedOverTls;
			_v20Compatibility = v20Compatibility;
		}
		
		public string ToHttpUrl() {
			if (_v20Compatibility && !string.IsNullOrEmpty(HostHeader)) {
				var scheme = SeedOverTls ? EndpointExtensions.HTTPS_SCHEMA : EndpointExtensions.HTTP_SCHEMA;

				return string.Format("{0}://{1}:{2}/gossip?format=json", scheme, HostHeader, EndPoint.Port);
			}
			
			return EndPoint.ToHttpUrl(SeedOverTls ? EndpointExtensions.HTTPS_SCHEMA : EndpointExtensions.HTTP_SCHEMA,
				"/gossip?format=json");
		}
		
		public string GetHostHeader() {
			return _v20Compatibility ? HostHeader : "";
		}
	}
}
