using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Management.ManagedProjectionStates {
	class RunningState : ManagedProjectionStateBase {
		public RunningState(ManagedProjection managedProjection)
			: base(managedProjection) {
		}

		protected internal override void Faulted(CoreProjectionStatusMessage.Faulted message) {
			SetFaulted(message.FaultedReason);
		}

		protected internal override void Started() {
			// do nothing - may mean second pahse started
			//TODO: stop sending second Started
		}

		protected internal override void Stopped(CoreProjectionStatusMessage.Stopped message) {
			if (message.Completed)
				_managedProjection.SetState(ManagedProjectionState.Completed);
			else
				base.Stopped(message);
		}
	}
}
