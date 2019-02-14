using System;
using System.Net;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Cluster {
	public class MemberInfo : IEquatable<MemberInfo> {
		public readonly Guid InstanceId;

		public readonly DateTime TimeStamp;
		public readonly VNodeState State;
		public readonly bool IsAlive;

		public readonly IPEndPoint InternalTcpEndPoint;
		public readonly IPEndPoint InternalSecureTcpEndPoint;
		public readonly IPEndPoint ExternalTcpEndPoint;
		public readonly IPEndPoint ExternalSecureTcpEndPoint;
		public readonly IPEndPoint InternalHttpEndPoint;
		public readonly IPEndPoint ExternalHttpEndPoint;

		public readonly long LastCommitPosition;
		public readonly long WriterCheckpoint;
		public readonly long ChaserCheckpoint;
		public readonly long EpochPosition;
		public readonly int EpochNumber;
		public readonly Guid EpochId;

		public readonly int NodePriority;

		public static MemberInfo ForManager(Guid instanceId, DateTime timeStamp, bool isAlive,
			IPEndPoint internalHttpEndPoint, IPEndPoint externalHttpEndPoint) {
			return new MemberInfo(instanceId, timeStamp, VNodeState.Manager, true,
				internalHttpEndPoint, null, externalHttpEndPoint, null,
				internalHttpEndPoint, externalHttpEndPoint,
				-1, -1, -1, -1, -1, Guid.Empty, 0);
		}

		public static MemberInfo ForVNode(Guid instanceId,
			DateTime timeStamp,
			VNodeState state,
			bool isAlive,
			IPEndPoint internalTcpEndPoint,
			IPEndPoint internalSecureTcpEndPoint,
			IPEndPoint externalTcpEndPoint,
			IPEndPoint externalSecureTcpEndPoint,
			IPEndPoint internalHttpEndPoint,
			IPEndPoint externalHttpEndPoint,
			long lastCommitPosition,
			long writerCheckpoint,
			long chaserCheckpoint,
			long epochPosition,
			int epochNumber,
			Guid epochId,
			int nodePriority) {
			if (state == VNodeState.Manager)
				throw new ArgumentException(string.Format("Wrong State for VNode: {0}", state), "state");
			return new MemberInfo(instanceId, timeStamp, state, isAlive,
				internalTcpEndPoint, internalSecureTcpEndPoint,
				externalTcpEndPoint, externalSecureTcpEndPoint,
				internalHttpEndPoint, externalHttpEndPoint,
				lastCommitPosition, writerCheckpoint, chaserCheckpoint,
				epochPosition, epochNumber, epochId, nodePriority);
		}

		private MemberInfo(Guid instanceId, DateTime timeStamp, VNodeState state, bool isAlive,
			IPEndPoint internalTcpEndPoint, IPEndPoint internalSecureTcpEndPoint,
			IPEndPoint externalTcpEndPoint, IPEndPoint externalSecureTcpEndPoint,
			IPEndPoint internalHttpEndPoint, IPEndPoint externalHttpEndPoint,
			long lastCommitPosition, long writerCheckpoint, long chaserCheckpoint,
			long epochPosition, int epochNumber, Guid epochId, int nodePriority) {
			Ensure.NotNull(internalTcpEndPoint, "internalTcpEndPoint");
			Ensure.NotNull(externalTcpEndPoint, "externalTcpEndPoint");
			Ensure.NotNull(internalHttpEndPoint, "internalHttpEndPoint");
			Ensure.NotNull(externalHttpEndPoint, "externalHttpEndPoint");

			InstanceId = instanceId;

			TimeStamp = timeStamp;
			State = state;
			IsAlive = isAlive;

			InternalTcpEndPoint = internalTcpEndPoint;
			InternalSecureTcpEndPoint = internalSecureTcpEndPoint;
			ExternalTcpEndPoint = externalTcpEndPoint;
			ExternalSecureTcpEndPoint = externalSecureTcpEndPoint;
			InternalHttpEndPoint = internalHttpEndPoint;
			ExternalHttpEndPoint = externalHttpEndPoint;

			LastCommitPosition = lastCommitPosition;
			WriterCheckpoint = writerCheckpoint;
			ChaserCheckpoint = chaserCheckpoint;

			EpochPosition = epochPosition;
			EpochNumber = epochNumber;
			EpochId = epochId;

			NodePriority = nodePriority;
		}

		internal MemberInfo(MemberInfoDto dto) {
			InstanceId = dto.InstanceId;
			TimeStamp = dto.TimeStamp;
			State = dto.State;
			IsAlive = dto.IsAlive;
			var internalTcpIp = IPAddress.Parse(dto.InternalTcpIp);
			var externalTcpIp = IPAddress.Parse(dto.ExternalTcpIp);
			InternalTcpEndPoint = new IPEndPoint(internalTcpIp, dto.InternalTcpPort);
			InternalSecureTcpEndPoint = dto.InternalSecureTcpPort > 0
				? new IPEndPoint(internalTcpIp, dto.InternalSecureTcpPort)
				: null;
			ExternalTcpEndPoint = new IPEndPoint(externalTcpIp, dto.ExternalTcpPort);
			ExternalSecureTcpEndPoint = dto.ExternalSecureTcpPort > 0
				? new IPEndPoint(externalTcpIp, dto.ExternalSecureTcpPort)
				: null;
			InternalHttpEndPoint = new IPEndPoint(IPAddress.Parse(dto.InternalHttpIp), dto.InternalHttpPort);
			ExternalHttpEndPoint = new IPEndPoint(IPAddress.Parse(dto.ExternalHttpIp), dto.ExternalHttpPort);
			LastCommitPosition = dto.LastCommitPosition;
			WriterCheckpoint = dto.WriterCheckpoint;
			ChaserCheckpoint = dto.ChaserCheckpoint;
			EpochPosition = dto.EpochPosition;
			EpochNumber = dto.EpochNumber;
			EpochId = dto.EpochId;
			NodePriority = dto.NodePriority;
		}

		public bool Is(IPEndPoint endPoint) {
			return endPoint != null
			       && (InternalHttpEndPoint.Equals(endPoint)
			           || ExternalHttpEndPoint.Equals(endPoint)
			           || InternalTcpEndPoint.Equals(endPoint)
			           || (InternalSecureTcpEndPoint != null && InternalSecureTcpEndPoint.Equals(endPoint))
			           || ExternalTcpEndPoint.Equals(endPoint)
			           || (ExternalSecureTcpEndPoint != null && ExternalSecureTcpEndPoint.Equals(endPoint)));
		}

		public MemberInfo Updated(VNodeState? state = null,
			bool? isAlive = null,
			long? lastCommitPosition = null,
			long? writerCheckpoint = null,
			long? chaserCheckpoint = null,
			EpochRecord epoch = null) {
			return new MemberInfo(InstanceId,
				DateTime.UtcNow,
				state ?? State,
				isAlive ?? IsAlive,
				InternalTcpEndPoint,
				InternalSecureTcpEndPoint,
				ExternalTcpEndPoint,
				ExternalSecureTcpEndPoint,
				InternalHttpEndPoint,
				ExternalHttpEndPoint,
				lastCommitPosition ?? LastCommitPosition,
				writerCheckpoint ?? WriterCheckpoint,
				chaserCheckpoint ?? ChaserCheckpoint,
				epoch != null ? epoch.EpochPosition : EpochPosition,
				epoch != null ? epoch.EpochNumber : EpochNumber,
				epoch != null ? epoch.EpochId : EpochId,
				NodePriority);
		}

		public override string ToString() {
			if (State == VNodeState.Manager)
				return string.Format("MAN {0:B} <{1}> [{2}, {3}, {4}] | {5:yyyy-MM-dd HH:mm:ss.fff}",
					InstanceId, IsAlive ? "LIVE" : "DEAD", State,
					InternalHttpEndPoint, ExternalHttpEndPoint, TimeStamp);
			return string.Format(
				"VND {0:B} <{1}> [{2}, {3}, {4}, {5}, {6}, {7}, {8}] {9}/{10}/{11}/E{12}@{13}:{14:B} | {15:yyyy-MM-dd HH:mm:ss.fff}",
				InstanceId, IsAlive ? "LIVE" : "DEAD", State,
				InternalTcpEndPoint, InternalSecureTcpEndPoint == null ? "n/a" : InternalSecureTcpEndPoint.ToString(),
				ExternalTcpEndPoint, ExternalSecureTcpEndPoint == null ? "n/a" : ExternalSecureTcpEndPoint.ToString(),
				InternalHttpEndPoint, ExternalHttpEndPoint,
				LastCommitPosition, WriterCheckpoint, ChaserCheckpoint,
				EpochNumber, EpochPosition, EpochId,
				TimeStamp);
		}

		public bool Equals(MemberInfo other) {
			// we ignore timestamp and checkpoints for equality comparison
			if (ReferenceEquals(null, other)) return false;
			if (ReferenceEquals(this, other)) return true;
			return other.InstanceId == InstanceId
			       && other.State == State
			       && other.IsAlive == IsAlive
			       && Equals(other.InternalTcpEndPoint, InternalTcpEndPoint)
			       && Equals(other.InternalSecureTcpEndPoint, InternalSecureTcpEndPoint)
			       && Equals(other.ExternalTcpEndPoint, ExternalTcpEndPoint)
			       && Equals(other.ExternalSecureTcpEndPoint, ExternalSecureTcpEndPoint)
			       && Equals(other.InternalHttpEndPoint, InternalHttpEndPoint)
			       && Equals(other.ExternalHttpEndPoint, ExternalHttpEndPoint)
			       && other.EpochPosition == EpochPosition
			       && other.EpochNumber == EpochNumber
			       && other.EpochId == EpochId
			       && other.NodePriority == NodePriority;
		}

		public override bool Equals(object obj) {
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != typeof(MemberInfo)) return false;
			return Equals((MemberInfo)obj);
		}

		public override int GetHashCode() {
			unchecked {
				int result = InstanceId.GetHashCode();
				result = (result * 397) ^ State.GetHashCode();
				result = (result * 397) ^ IsAlive.GetHashCode();
				result = (result * 397) ^ InternalTcpEndPoint.GetHashCode();
				result = (result * 397) ^
				         (InternalSecureTcpEndPoint != null ? InternalSecureTcpEndPoint.GetHashCode() : 0);
				result = (result * 397) ^ ExternalTcpEndPoint.GetHashCode();
				result = (result * 397) ^
				         (ExternalSecureTcpEndPoint != null ? ExternalSecureTcpEndPoint.GetHashCode() : 0);
				result = (result * 397) ^ InternalHttpEndPoint.GetHashCode();
				result = (result * 397) ^ ExternalHttpEndPoint.GetHashCode();
				result = (result * 397) ^ EpochPosition.GetHashCode();
				result = (result * 397) ^ EpochNumber.GetHashCode();
				result = (result * 397) ^ EpochId.GetHashCode();
				result = (result * 397) ^ NodePriority;
				return result;
			}
		}
	}
}
