using System;

namespace EventStore.Client {
	public class ClusterMessages {
		public class ClusterInfo {
			public MemberInfo[] Members { get; set; }
		}

		public class MemberInfo {
			public Guid InstanceId { get; set; }

			public VNodeState State { get; set; }
			public bool IsAlive { get; set; }
			public string ExternalHttpIp { get; set; }
			public int ExternalHttpPort { get; set; }
		}

		public enum VNodeState {
			Initializing,
			Unknown,
			PreReplica,
			CatchingUp,
			Clone,
			Follower,
			PreLeader,
			Leader,
			Manager,
			ShuttingDown,
			Shutdown,
			ReadOnlyLeaderless,
			PreReadOnlyReplica,
			ReadOnlyReplica,
			ResigningLeader
		}
	}
}
