namespace EventStore.Projections.Core.Services.Management.ManagedProjectionStates
{
    class CompletedState : ManagedProjection.ManagedProjectionStateBase
    {
        public CompletedState(ManagedProjection managedProjection)
            : base(managedProjection)
        {
        }

    }
}