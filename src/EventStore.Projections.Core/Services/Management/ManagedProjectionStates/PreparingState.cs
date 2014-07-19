using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Management.ManagedProjectionStates
{
    class PreparingState : ManagedProjection.ManagedProjectionStateBase
    {
        public PreparingState(ManagedProjection managedProjection)
            : base(managedProjection)
        {
        }

        protected internal override void Faulted(CoreProjectionStatusMessage.Faulted message)
        {
            SetFaulted(message.FaultedReason);
            _managedProjection._persistedState.SourceDefinition = null;
            _managedProjection.WriteStartOrLoadStopped();
        }

        protected internal override void Prepared(CoreProjectionStatusMessage.Prepared message)
        {
            _managedProjection.SetState(ManagedProjectionState.Prepared);
            _managedProjection._persistedState.SourceDefinition = message.SourceDefinition;
            _managedProjection._prepared = true;
            _managedProjection._created = true;
            _managedProjection.WriteStartOrLoadStopped();
        }
    }
}