namespace EventStore.Projections.Core.Services.Management.ManagedProjectionStates {
	class FaultedState : ManagedProjectionStateBase {
		public FaultedState(ManagedProjection managedProjection)
			: base(managedProjection) {
		}
	}
}
