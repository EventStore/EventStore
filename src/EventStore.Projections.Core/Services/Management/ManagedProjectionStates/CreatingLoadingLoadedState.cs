namespace EventStore.Projections.Core.Services.Management.ManagedProjectionStates
{
    class CreatingLoadingLoadedState : ManagedProjection.ManagedProjectionStateBase
    {
        public CreatingLoadingLoadedState(ManagedProjection managedProjection)
            : base(managedProjection)
        {
        }

    }
}