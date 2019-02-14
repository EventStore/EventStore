using System.Linq;
using System.Net;
using EventStore.Core.Cluster;

namespace EventStore.Core.Messages {
	public class ClusterInfoDto {
		public MemberInfoDto[] Members { get; set; }
		public string ServerIp { get; set; }
		public int ServerPort { get; set; }

		public ClusterInfoDto() {
		}

		public ClusterInfoDto(ClusterInfo clusterInfo, IPEndPoint serverEndPoint) {
			Members = clusterInfo.Members.Select(x => new MemberInfoDto(x)).ToArray();
			ServerIp = serverEndPoint.Address.ToString();
			ServerPort = serverEndPoint.Port;
		}

		public override string ToString() {
			return string.Format("Server: {0}:{1}, Members: [{2}]",
				ServerIp, ServerPort,
				Members != null ? string.Join(",", Members.Select(m => m.ToString())) : "null");
		}
	}
}
