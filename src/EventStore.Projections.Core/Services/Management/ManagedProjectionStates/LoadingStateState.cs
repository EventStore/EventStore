using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Management.ManagedProjectionStates {
	class LoadingStateState : ManagedProjectionStateBase {
		public LoadingStateState(ManagedProjection managedProjection)
			: base(managedProjection) {
		}

		protected internal override void Stopped(CoreProjectionStatusMessage.Stopped message) {
			_managedProjection.SetState(ManagedProjectionState.Stopped);
			_managedProjection.PrepareOrWriteStartOrLoadStopped();
		}

		protected internal override void Faulted(CoreProjectionStatusMessage.Faulted message) {
			SetFaulted(message.FaultedReason);
			_managedProjection.PrepareOrWriteStartOrLoadStopped();
		}
	}
}
