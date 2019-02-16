using System;
using EventStore.Core.Cluster;
using EventStore.Core.Data;

namespace EventStore.Core.Messages {
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

		public MemberInfoDto() {
		}

		public MemberInfoDto(MemberInfo member) {
			InstanceId = member.InstanceId;

			TimeStamp = member.TimeStamp;
			State = member.State;
			IsAlive = member.IsAlive;

			InternalTcpIp = member.InternalTcpEndPoint.Address.ToString();
			InternalTcpPort = member.InternalTcpEndPoint.Port;
			InternalSecureTcpPort =
				member.InternalSecureTcpEndPoint == null ? 0 : member.InternalSecureTcpEndPoint.Port;

			ExternalTcpIp = member.ExternalTcpEndPoint.Address.ToString();
			ExternalTcpPort = member.ExternalTcpEndPoint.Port;
			ExternalSecureTcpPort =
				member.ExternalSecureTcpEndPoint == null ? 0 : member.ExternalSecureTcpEndPoint.Port;

			InternalHttpIp = member.InternalHttpEndPoint.Address.ToString();
			InternalHttpPort = member.InternalHttpEndPoint.Port;

			ExternalHttpIp = member.ExternalHttpEndPoint.Address.ToString();
			ExternalHttpPort = member.ExternalHttpEndPoint.Port;

			LastCommitPosition = member.LastCommitPosition;
			WriterCheckpoint = member.WriterCheckpoint;
			ChaserCheckpoint = member.ChaserCheckpoint;

			EpochPosition = member.EpochPosition;
			EpochNumber = member.EpochNumber;
			EpochId = member.EpochId;

			NodePriority = member.NodePriority;
		}

		public override string ToString() {
			return string.Format("InstanceId: {0:B}, TimeStamp: {1:yyyy-MM-dd HH:mm:ss.fff}, State: {2}, IsAlive: {3}, "
			                     + "InternalTcpIp: {4}, InternalTcpPort: {5}, InternalSecureTcpPort: {6}, "
			                     + "ExternalTcpIp: {7}, ExternalTcpPort: {8}, ExternalSecureTcpPort: {9}, "
			                     + "InternalHttpIp: {10}, InternalHttpPort: {11}, ExternalHttpIp: {12}, ExternalHttpPort: {13}, "
			                     + "LastCommitPosition: {14}, WriterCheckpoint: {15}, ChaserCheckpoint: {16}, "
			                     + "EpochPosition: {17}, EpochNumber: {18}, EpochId: {19:B}, NodePriority: {20}",
				InstanceId, TimeStamp, State, IsAlive,
				InternalTcpIp, InternalTcpPort, InternalSecureTcpPort,
				ExternalTcpIp, ExternalTcpPort, ExternalSecureTcpPort,
				InternalHttpIp, InternalHttpPort, ExternalHttpIp, ExternalHttpPort,
				LastCommitPosition, WriterCheckpoint, ChaserCheckpoint,
				EpochPosition, EpochNumber, EpochId, NodePriority);
		}
	}
}
