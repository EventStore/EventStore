// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Management.ManagedProjectionStates {
	class StoppingState : ManagedProjectionStateBase {
		public StoppingState(ManagedProjection managedProjection)
			: base(managedProjection) {
		}

		protected internal override void Stopped(CoreProjectionStatusMessage.Stopped message) {
			_managedProjection.SetState(
				message.Completed ? ManagedProjectionState.Completed : ManagedProjectionState.Stopped);
			_managedProjection.PrepareOrWriteStartOrLoadStopped();
		}

		protected internal override void Faulted(CoreProjectionStatusMessage.Faulted message) {
			SetFaulted(message.FaultedReason);
			_managedProjection.PrepareOrWriteStartOrLoadStopped();
		}
	}
}
