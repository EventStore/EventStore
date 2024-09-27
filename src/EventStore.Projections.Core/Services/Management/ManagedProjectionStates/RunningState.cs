// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

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
