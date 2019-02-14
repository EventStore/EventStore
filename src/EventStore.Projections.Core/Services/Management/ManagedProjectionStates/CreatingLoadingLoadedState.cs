namespace EventStore.Projections.Core.Services.Management.ManagedProjectionStates {
	class CreatingLoadingLoadedState : ManagedProjectionStateBase {
		public CreatingLoadingLoadedState(ManagedProjection managedProjection)
			: base(managedProjection) {
		}
	}
}
