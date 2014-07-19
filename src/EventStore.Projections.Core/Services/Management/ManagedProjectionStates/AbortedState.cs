namespace EventStore.Projections.Core.Services.Management.ManagedProjectionStates
{
    class AbortedState : ManagedProjection.ManagedProjectionStateBase
    {
        public AbortedState(ManagedProjection managedProjection)
            : base(managedProjection)
        {
        }

    }
}