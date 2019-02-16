namespace EventStore.Projections.Core.Services.Management.ManagedProjectionStates {
	class CompletedState : ManagedProjectionStateBase {
		public CompletedState(ManagedProjection managedProjection)
			: base(managedProjection) {
		}
	}
}
