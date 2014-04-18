namespace EventStore.Projections.Core.Services.Management.ManagedProjectionStates
{
    class PreparedState : ManagedProjection.ManagedProjectionStateBase
    {
        public PreparedState(ManagedProjection managedProjection)
            : base(managedProjection)
        {
        }

    }
}