using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Management.ManagedProjectionStates {
	class StartingState : ManagedProjectionStateBase {
		public StartingState(ManagedProjection managedProjection)
			: base(managedProjection) {
		}

		protected internal override void Started() {
			_managedProjection.SetState(ManagedProjectionState.Running);
			_managedProjection.StartCompleted();
		}

		protected internal override void Faulted(CoreProjectionStatusMessage.Faulted message) {
			SetFaulted(message.FaultedReason);
			_managedProjection.StartCompleted();
		}
	}
}
