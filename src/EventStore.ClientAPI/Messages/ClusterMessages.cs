using System;

namespace EventStore.ClientAPI.Messages {
	internal class ClusterMessages {
		public class ClusterInfoDto {
			public MemberInfoDto[] Members { get; set; }

			public ClusterInfoDto() {
			}

			public ClusterInfoDto(MemberInfoDto[] members) {
				Members = members;
			}
		}

		public class MemberInfoDto {
			public Guid InstanceId { get; set; }

			public DateTime TimeStamp { get; set; }
			public VNodeState State { get; set; }
			public bool IsAlive { get; set; }

			public string InternalTcpIp { get; set; }
			public int InternalTcpPort { get; set; }
			public int InternalSecureTcpPort { get; set; }

			public string ExternalTcpIp { get; set; }
			public int ExternalTcpPort { get; set; }
			public int ExternalSecureTcpPort { get; set; }

			public string HttpAddress => HttpEndPointIp ?? ExternalHttpIp;
			public int HttpPort => HttpEndPointPort != default ? HttpEndPointPort : ExternalHttpPort;

			// 20.x cluster info
			public string HttpEndPointIp { get; set; }
			public int HttpEndPointPort { get; set; }
			
			// 5.x cluster info
			public string ExternalHttpIp { get; set; }
			public int ExternalHttpPort { get; set; }

			public long LastCommitPosition { get; set; }
			public long WriterCheckpoint { get; set; }
			public long ChaserCheckpoint { get; set; }

			public long EpochPosition { get; set; }
			public int EpochNumber { get; set; }
			public Guid EpochId { get; set; }

			public int NodePriority { get; set; }

			public override string ToString() {
				if (State == VNodeState.Manager)
					return string.Format("MAN {0:B} <{1}> [{2}, {3}:{4}] | {5:yyyy-MM-dd HH:mm:ss.fff}",
						InstanceId, IsAlive ? "LIVE" : "DEAD", State,
						HttpAddress, HttpPort,
						TimeStamp);
				return string.Format(
					"VND {0:B} <{1}> [{2}, {3}:{4}, {5}, {6}:{7}, {8}, {9}:{10}] {11}/{12}/{13}/E{14}@{15}:{16:B} | {17:yyyy-MM-dd HH:mm:ss.fff}",
					InstanceId, IsAlive ? "LIVE" : "DEAD", State,
					InternalTcpIp, InternalTcpPort,
					InternalSecureTcpPort > 0 ? string.Format("{0}:{1}", InternalTcpIp, InternalSecureTcpPort) : "n/a",
					ExternalTcpIp, ExternalTcpPort,
					ExternalSecureTcpPort > 0 ? string.Format("{0}:{1}", ExternalTcpIp, ExternalSecureTcpPort) : "n/a",
					HttpAddress, HttpPort,
					LastCommitPosition, WriterCheckpoint, ChaserCheckpoint,
					EpochNumber, EpochPosition, EpochId,
					TimeStamp);
			}
		}

		//The order specified in this enum is important.
		//It defines the default sorting order (descending, ignoring a few specific states) used in ClusterDnsEndPointDiscoverer when choosing which node the client should connect to.
		public enum VNodeState {
			Initializing = 1,
			ReadOnlyLeaderless = 2,
			Unknown = 3,
			PreReadOnlyReplica = 4,
			PreReplica = 5,
			CatchingUp = 6,
			Clone = 7,
			ReadOnlyReplica = 8,
			Slave = 9,
			Follower = 10,
			PreMaster = 11,
			PreLeader = 12,
			Master = 13,
			Leader = 14,
			Manager = 15,
			ShuttingDown = 16,
			Shutdown = 17
		}
	}
}
