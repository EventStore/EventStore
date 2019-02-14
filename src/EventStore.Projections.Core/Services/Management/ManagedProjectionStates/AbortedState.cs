namespace EventStore.Projections.Core.Services.Management.ManagedProjectionStates {
	class AbortedState : ManagedProjectionStateBase {
		public AbortedState(ManagedProjection managedProjection)
			: base(managedProjection) {
		}
	}
}
