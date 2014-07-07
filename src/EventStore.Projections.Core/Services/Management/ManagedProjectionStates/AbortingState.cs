using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Management.ManagedProjectionStates
{
    class AbortingState : ManagedProjection.ManagedProjectionStateBase
    {
        public AbortingState(ManagedProjection managedProjection)
            : base(managedProjection)
        {
        }

        protected internal override void Stopped(CoreProjectionStatusMessage.Stopped message)
        {
            _managedProjection.SetState(ManagedProjectionState.Aborted);
            _managedProjection.StoppedOrReadyToStart();
        }

        protected internal override void Faulted(CoreProjectionStatusMessage.Faulted message)
        {
            _managedProjection.SetState(ManagedProjectionState.Aborted);
            _managedProjection.StoppedOrReadyToStart();
        }

    }
}