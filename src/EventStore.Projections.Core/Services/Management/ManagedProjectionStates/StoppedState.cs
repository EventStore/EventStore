namespace EventStore.Projections.Core.Services.Management.ManagedProjectionStates {
	class StoppedState : ManagedProjectionStateBase {
		public StoppedState(ManagedProjection managedProjection)
			: base(managedProjection) {
		}
	}
}
