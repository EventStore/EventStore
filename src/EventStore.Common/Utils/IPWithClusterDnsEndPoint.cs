using System.Net;

namespace EventStore.Common.Utils {
	public class IPWithClusterDnsEndPoint : IPEndPoint {
		public string ClusterDnsName { get; }

		public IPWithClusterDnsEndPoint(IPAddress address, string clusterDns, int port) : base(address, port) {
			Ensure.NotNull(clusterDns, "clusterDns");
			Ensure.NotNull(address, "address");
			ClusterDnsName = clusterDns;
		}

		public override string ToString() => $"{base.ToString()}/{ClusterDnsName}";
	}
}
