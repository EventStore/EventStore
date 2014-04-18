namespace EventStore.Projections.Core.Services.Management.ManagedProjectionStates
{
    class WritingState : ManagedProjection.ManagedProjectionStateBase
    {
        public WritingState(ManagedProjection managedProjection)
            : base(managedProjection)
        {
        }

    }
}