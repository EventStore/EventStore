using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using EventStore.Client;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Messages;

namespace EventStore.Core.Cluster {
	public class ClusterInfo {
		private static readonly IPEndPointComparer Comparer = new IPEndPointComparer();

		public readonly MemberInfo[] Members;

		public ClusterInfo(params MemberInfo[] members) : this((IEnumerable<MemberInfo>)members) {
		}

		public ClusterInfo(IEnumerable<MemberInfo> members) {
			Members = members.Safe().OrderByDescending<MemberInfo, IPEndPoint>(x => x.InternalHttpEndPoint, Comparer)
				.ToArray();
		}

		public ClusterInfo(ClusterInfoDto dto) {
			Members = dto.Members.Safe().Select(x => new MemberInfo(x))
				.OrderByDescending<MemberInfo, IPEndPoint>(x => x.InternalHttpEndPoint, Comparer).ToArray();
		}

		public override string ToString() {
			return string.Join("\n", Members.Select(s => s.ToString()));
		}

		public bool HasChangedSince(ClusterInfo other) {
			if (ReferenceEquals(null, other)) return true;
			if (ReferenceEquals(this, other)) return false;

			if (other.Members.Length != Members.Length)
				return true;

			for (int i = 0; i < Members.Length; i++) {
				if (!Members[i].Equals(other.Members[i]))
					return true;
			}

			return false;
		}
		
		public static ClusterInfo FromGrpcClusterInfo(EventStore.Cluster.ClusterInfo grpcCluster) {
			var receivedMembers = Array.ConvertAll(grpcCluster.Members.ToArray(), x =>
				new MemberInfo(
					Uuid.FromDto(x.InstanceId).ToGuid(), x.TimeStamp.FromTicksSinceEpoch(), (VNodeState)x.State,
					x.IsAlive,
					new IPEndPoint(IPAddress.Parse(x.InternalTcp.Address), (int)x.InternalTcp.Port),
					new IPEndPoint(IPAddress.Parse(x.ExternalTcp.Address), (int)x.ExternalTcp.Port),
					new IPEndPoint(IPAddress.Parse(x.InternalHttp.Address), (int)x.InternalHttp.Port),
					new IPEndPoint(IPAddress.Parse(x.ExternalHttp.Address), (int)x.ExternalHttp.Port),
					x.LastCommitPosition, x.WriterCheckpoint, x.ChaserCheckpoint,
					x.EpochPosition, x.EpochNumber, Uuid.FromDto(x.EpochId).ToGuid(), x.NodePriority,
					x.IsReadOnlyReplica
				)).ToArray();
			return new ClusterInfo(receivedMembers);
		}

		public static EventStore.Cluster.ClusterInfo ToGrpcClusterInfo(ClusterInfo cluster) {
			var members = Array.ConvertAll(cluster.Members, x => new EventStore.Cluster.MemberInfo {
				InstanceId = Uuid.FromGuid(x.InstanceId).ToDto(),
				TimeStamp = x.TimeStamp.ToTicksSinceEpoch(),
				State = (EventStore.Cluster.MemberInfo.Types.VNodeState)x.State,
				IsAlive = x.IsAlive,
				ExternalHttp = new EventStore.Cluster.EndPoint(
					x.ExternalHttpEndPoint.Address.ToString(),
					(uint)x.ExternalHttpEndPoint.Port),
				InternalHttp = new EventStore.Cluster.EndPoint(
					x.InternalHttpEndPoint.Address.ToString(),
					(uint)x.InternalHttpEndPoint.Port),
				InternalTcp = new EventStore.Cluster.EndPoint(
					x.InternalTcpEndPoint.Address.ToString(),
					(uint)x.InternalTcpEndPoint.Port),
				ExternalTcp = new EventStore.Cluster.EndPoint(
					x.ExternalTcpEndPoint.Address.ToString(),
					(uint)x.ExternalTcpEndPoint.Port),
				LastCommitPosition = x.LastCommitPosition,
				WriterCheckpoint = x.WriterCheckpoint,
				ChaserCheckpoint = x.ChaserCheckpoint,
				EpochPosition = x.EpochPosition,
				EpochNumber = x.EpochNumber,
				EpochId = Uuid.FromGuid(x.EpochId).ToDto(),
				NodePriority = x.NodePriority,
				IsReadOnlyReplica = x.IsReadOnlyReplica
			}).ToArray();
			var info = new EventStore.Cluster.ClusterInfo();
			info.Members.AddRange(members);
			return info;
		}
	}
}
