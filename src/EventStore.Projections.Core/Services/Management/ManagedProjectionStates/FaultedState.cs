namespace EventStore.Projections.Core.Services.Management.ManagedProjectionStates
{
    class FaultedState : ManagedProjection.ManagedProjectionStateBase
    {
        public FaultedState(ManagedProjection managedProjection)
            : base(managedProjection)
        {
        }

    }
}