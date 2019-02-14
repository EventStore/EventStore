namespace EventStore.Projections.Core.Services.Management {
	public enum ManagedProjectionState {
		Creating,
		Loading,
		Loaded,
		Preparing,
		Prepared,
		Starting,
		LoadingStopped,
		Running,
		Stopping,
		Aborting,
		Stopped,
		Completed,
		Aborted,
		Faulted,
		Deleting,
	}
}
