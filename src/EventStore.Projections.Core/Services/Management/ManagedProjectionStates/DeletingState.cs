namespace EventStore.Projections.Core.Services.Management.ManagedProjectionStates {
	class DeletingState : ManagedProjectionStateBase {
		public DeletingState(ManagedProjection managedProjection)
			: base(managedProjection) {
			managedProjection.DeleteProjectionStreams();
		}
	}
}
