using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using EventStore.Client;
using EventStore.Common;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Grpc;

namespace EventStore.Core.Cluster {
	public class ClusterInfo {
		private static readonly EndPointComparer Comparer = new EndPointComparer();

		public readonly MemberInfo[] Members;

		public ClusterInfo(params MemberInfo[] members) : this((IEnumerable<MemberInfo>)members) {
		}

		public ClusterInfo(IEnumerable<MemberInfo> members) {
			Members = members.Safe().OrderByDescending<MemberInfo, EndPoint>(x => x.InternalHttpEndPoint, Comparer)
				.ToArray();
		}

		public ClusterInfo(ClusterInfoDto dto) {
			Members = dto.Members.Safe().Select(x => new MemberInfo(x))
				.OrderByDescending<MemberInfo, EndPoint>(x => x.InternalHttpEndPoint, Comparer).ToArray();
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
					!x.InternalTcpUsesTls ? new EventStoreEndPoint(x.InternalTcp.Address, (int)x.InternalTcp.Port) : null,
					x.InternalTcpUsesTls ? new EventStoreEndPoint(x.InternalTcp.Address, (int)x.InternalTcp.Port) : null,
					!x.ExternalTcpUsesTls && x.ExternalTcp != null ? new EventStoreEndPoint(x.ExternalTcp.Address, (int)x.ExternalTcp.Port) : null,
					x.ExternalTcpUsesTls && x.ExternalTcp != null ? new EventStoreEndPoint(x.ExternalTcp.Address, (int)x.ExternalTcp.Port) : null,
					new EventStoreEndPoint(x.InternalHttp.Address, (int)x.InternalHttp.Port),
					new EventStoreEndPoint(x.ExternalHttp.Address, (int)x.ExternalHttp.Port),
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
					x.ExternalHttpEndPoint.GetHost(),
					(uint)x.ExternalHttpEndPoint.GetPort()),
				InternalHttp = new EventStore.Cluster.EndPoint(
					x.InternalHttpEndPoint.GetHost(),
					(uint)x.InternalHttpEndPoint.GetPort()),
				InternalTcp = x.InternalSecureTcpEndPoint != null ?
					new EventStore.Cluster.EndPoint(
						x.InternalSecureTcpEndPoint.GetHost(),
						(uint)x.InternalSecureTcpEndPoint.GetPort()) :
					new EventStore.Cluster.EndPoint(
					x.InternalTcpEndPoint.GetHost(),
					(uint)x.InternalTcpEndPoint.GetPort()),
				InternalTcpUsesTls = x.InternalSecureTcpEndPoint != null,
				ExternalTcp = x.ExternalSecureTcpEndPoint != null ?
					new EventStore.Cluster.EndPoint(
						x.ExternalSecureTcpEndPoint.GetHost(),
						(uint)x.ExternalSecureTcpEndPoint.GetPort()) :
					x.ExternalTcpEndPoint != null ?
					new EventStore.Cluster.EndPoint(
					x.ExternalTcpEndPoint.GetHost(),
					(uint)x.ExternalTcpEndPoint.GetPort()) : null,
				ExternalTcpUsesTls = x.ExternalSecureTcpEndPoint != null,
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
