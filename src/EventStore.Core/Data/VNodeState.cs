namespace EventStore.Core.Data {
	public enum VNodeState {
		Initializing,
		Unknown,
		PreReplica,
		CatchingUp,
		Clone,
		Slave,
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

	public static class VNodeStateExtensions {
		public static bool IsReplica(this VNodeState state) {
			return state == VNodeState.CatchingUp
			       || state == VNodeState.Clone
			       || state == VNodeState.Slave
				   || state == VNodeState.ReadOnlyReplica;
		}
	}
}
