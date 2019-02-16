namespace EventStore.Core.Data {
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

	public static class VNodeStateExtensions {
		public static bool IsReplica(this VNodeState state) {
			return state == VNodeState.CatchingUp
			       || state == VNodeState.Clone
			       || state == VNodeState.Slave;
		}
	}
}
