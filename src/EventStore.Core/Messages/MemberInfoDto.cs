using System;
using EventStore.Common.Utils;
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

		public string HttpEndPointIp { get; set; }
		public int HttpEndPointPort { get; set; }
		public string AdvertiseHostToClientAs { get; set; }
		public int AdvertiseHttpPortToClientAs { get; set; }
		public int AdvertiseTcpPortToClientAs { get; set; }

		public long LastCommitPosition { get; set; }
		public long WriterCheckpoint { get; set; }
		public long ChaserCheckpoint { get; set; }

		public long EpochPosition { get; set; }
		public int EpochNumber { get; set; }
		public Guid EpochId { get; set; }

		public int NodePriority { get; set; }
		public bool IsReadOnlyReplica { get; set; }

		public MemberInfoDto() {
		}

		public MemberInfoDto(MemberInfo member) {
			InstanceId = member.InstanceId;

			TimeStamp = member.TimeStamp;
			State = member.State;
			IsAlive = member.IsAlive;

			InternalTcpIp = member.InternalTcpEndPoint?.GetHost() ?? member.InternalSecureTcpEndPoint?.GetHost();
			InternalTcpPort = member.InternalTcpEndPoint == null ? 0 : member.InternalTcpEndPoint.GetPort();
			InternalSecureTcpPort =
				member.InternalSecureTcpEndPoint == null ? 0 : member.InternalSecureTcpEndPoint.GetPort();

			HttpEndPointIp = member.HttpEndPoint.GetHost();
			HttpEndPointPort = member.HttpEndPoint.GetPort();
			AdvertiseHostToClientAs = member.AdvertiseHostToClientAs;
			AdvertiseHttpPortToClientAs = member.AdvertiseHttpPortToClientAs;
			AdvertiseTcpPortToClientAs = member.AdvertiseTcpPortToClientAs;

			LastCommitPosition = member.LastCommitPosition;
			WriterCheckpoint = member.WriterCheckpoint;
			ChaserCheckpoint = member.ChaserCheckpoint;

			EpochPosition = member.EpochPosition;
			EpochNumber = member.EpochNumber;
			EpochId = member.EpochId;

			NodePriority = member.NodePriority;
			IsReadOnlyReplica = member.IsReadOnlyReplica;
		}

		public override string ToString() {
			return
				$"InstanceId: {InstanceId:B}, TimeStamp: {TimeStamp:yyyy-MM-dd HH:mm:ss.fff}, State: {State}, IsAlive: {IsAlive}, " +
				$"InternalTcpIp: {InternalTcpIp}, InternalTcpPort: {InternalTcpPort}, InternalSecureTcpPort: {InternalSecureTcpPort}, " +
				$"HttpEndPointIp: {HttpEndPointIp}, HttpEndPointPort: {HttpEndPointPort}, " +
				$"{nameof(AdvertiseHostToClientAs)}: {AdvertiseHostToClientAs}, {nameof(AdvertiseHttpPortToClientAs)}: {AdvertiseHttpPortToClientAs}, " +
				$"{nameof(AdvertiseTcpPortToClientAs)}: {AdvertiseTcpPortToClientAs}, " +
				$"LastCommitPosition: {LastCommitPosition}, WriterCheckpoint: {WriterCheckpoint}, ChaserCheckpoint: {ChaserCheckpoint}, " +
				$"EpochPosition: {EpochPosition}, EpochNumber: {EpochNumber}, EpochId: {EpochId:B}, NodePriority: {NodePriority}, " +
				$"IsReadOnlyReplica: {IsReadOnlyReplica}";
		}
	}
}
