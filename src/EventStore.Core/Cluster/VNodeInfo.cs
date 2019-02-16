using EventStore.Core.Data;

namespace EventStore.Core.Cluster {
	public static class VNodeInfoHelper {
		public static VNodeInfo FromMemberInfo(MemberInfo member) {
			return new VNodeInfo(member.InstanceId, 0,
				member.InternalTcpEndPoint, member.InternalSecureTcpEndPoint,
				member.ExternalTcpEndPoint, member.ExternalSecureTcpEndPoint,
				member.InternalHttpEndPoint, member.ExternalHttpEndPoint);
		}
	}
}
