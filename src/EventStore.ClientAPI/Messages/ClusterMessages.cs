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

			public string InternalHttpIp { get; set; }
			public int InternalHttpPort { get; set; }

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
					return string.Format("MAN {0:B} <{1}> [{2}, {3}:{4}, {5}:{6}] | {7:yyyy-MM-dd HH:mm:ss.fff}",
						InstanceId, IsAlive ? "LIVE" : "DEAD", State,
						InternalHttpIp, InternalHttpPort,
						ExternalHttpIp, ExternalHttpPort,
						TimeStamp);
				return string.Format(
					"VND {0:B} <{1}> [{2}, {3}:{4}, {5}, {6}:{7}, {8}, {9}:{10}, {11}:{12}] {13}/{14}/{15}/E{16}@{17}:{18:B} | {19:yyyy-MM-dd HH:mm:ss.fff}",
					InstanceId, IsAlive ? "LIVE" : "DEAD", State,
					InternalTcpIp, InternalTcpPort,
					InternalSecureTcpPort > 0 ? string.Format("{0}:{1}", InternalTcpIp, InternalSecureTcpPort) : "n/a",
					ExternalTcpIp, ExternalTcpPort,
					ExternalSecureTcpPort > 0 ? string.Format("{0}:{1}", ExternalTcpIp, ExternalSecureTcpPort) : "n/a",
					InternalHttpIp, InternalHttpPort, ExternalHttpIp, ExternalHttpPort,
					LastCommitPosition, WriterCheckpoint, ChaserCheckpoint,
					EpochNumber, EpochPosition, EpochId,
					TimeStamp);
			}
		}

		public enum VNodeState {
			Initializing,
			Unknown,
			PreReplica,
			CatchingUp,
			Clone,
			Slave,
			PreMaster,
			Master,
			Manager,
			ShuttingDown,
			Shutdown
		}
	}
}
