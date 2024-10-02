// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

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
