namespace EventStore.Projections.Core.Services.Management.ManagedProjectionStates
{
    class StoppedState : ManagedProjection.ManagedProjectionStateBase
    {
        public StoppedState(ManagedProjection managedProjection)
            : base(managedProjection)
        {
        }

    }
}