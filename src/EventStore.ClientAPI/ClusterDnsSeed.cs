using System.Net;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Transport.Http;

namespace EventStore.ClientAPI {
	public class ClusterDnsSeed: IGossipSeed {
		private readonly string _clusterDns;
		private readonly bool _seedOverTls;
		private readonly bool _v20Compatibility;
		private readonly int _port;

		public ClusterDnsSeed(string clusterDns, int port, bool seedOverTls = false, bool v20Compatibility = false) {
			_clusterDns = clusterDns;
			_seedOverTls = seedOverTls;
			_v20Compatibility = v20Compatibility;
			_port = port;
		}

		public string ToHttpUrl() {
			if (_v20Compatibility) {
				var scheme = _seedOverTls ? EndpointExtensions.HTTPS_SCHEMA : EndpointExtensions.HTTP_SCHEMA;

				return string.Format("{0}://{1}:{2}/gossip?format=json", scheme, _clusterDns, _port);
			}

			var endpoint = new IPEndPoint(Resolution.Resolve(_clusterDns), _port);

			return endpoint.ToHttpUrl(_seedOverTls ? EndpointExtensions.HTTPS_SCHEMA : EndpointExtensions.HTTP_SCHEMA,
				"/gossip?format=json");
		}

		public string GetHostHeader() {
			return _v20Compatibility ? _clusterDns : "";
		}
	}
}
