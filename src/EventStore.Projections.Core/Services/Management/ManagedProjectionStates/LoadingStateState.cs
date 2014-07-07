using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Management.ManagedProjectionStates
{
    class LoadingStateState : ManagedProjection.ManagedProjectionStateBase
    {
        public LoadingStateState(ManagedProjection managedProjection)
            : base(managedProjection)
        {
        }

        protected internal override void Stopped(CoreProjectionStatusMessage.Stopped message)
        {
            _managedProjection.SetState(ManagedProjectionState.Stopped);
            _managedProjection.StoppedOrReadyToStart();
        }

        protected internal override void Faulted(CoreProjectionStatusMessage.Faulted message)
        {
            SetFaulted(message.FaultedReason);
            _managedProjection.StoppedOrReadyToStart();
        }
    }
}