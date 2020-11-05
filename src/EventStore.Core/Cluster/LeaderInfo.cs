namespace EventStore.Core.Cluster {
	public class LeaderInfo : MemberInfo {
		public readonly int? Term;

		public LeaderInfo(MemberInfo memberInfo, int? term) :
			base(memberInfo.InstanceId, memberInfo.TimeStamp, memberInfo.State, memberInfo.IsAlive, memberInfo.InternalTcpEndPoint,
				memberInfo.InternalSecureTcpEndPoint, memberInfo.ExternalTcpEndPoint, memberInfo.ExternalSecureTcpEndPoint,
				memberInfo.HttpEndPoint, memberInfo.AdvertiseHostToClientAs, memberInfo.AdvertiseHttpPortToClientAs,
				memberInfo.AdvertiseTcpPortToClientAs, memberInfo.LastCommitPosition, memberInfo.WriterCheckpoint,
				memberInfo.ChaserCheckpoint, memberInfo.EpochPosition, memberInfo.EpochNumber, memberInfo.EpochId,
				memberInfo.NodePriority, memberInfo.IsReadOnlyReplica) {
			Term = term;
		}

		public override int GetHashCode() {
			int result = Term?.GetHashCode() ?? 0;
			result = (result * 397) ^ base.GetHashCode();
			return result;
		}
	}
}
