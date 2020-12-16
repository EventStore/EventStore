namespace EventStore.ClientAPI.Internal {
	public class ClusterDnsSeed : IGossipSeed {
		private readonly string _clusterDns;
		private readonly bool _secure;
		
		public ClusterDnsSeed(string clusterDns, bool secure) {
			_clusterDns = clusterDns;
			_secure = secure;
		}

		public string ToHttpUrl() {
			var scheme = _secure ? "https" : "http";

			return string.Format("{0}://{1}/gossip?format=json", scheme, _clusterDns);
		}

		public string GetHostHeader() {
			return "";
		}
	}
}
