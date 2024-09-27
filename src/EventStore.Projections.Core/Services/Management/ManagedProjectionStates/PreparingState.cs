// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Management.ManagedProjectionStates {
	class PreparingState : ManagedProjectionStateBase {
		public PreparingState(ManagedProjection managedProjection)
			: base(managedProjection) {
		}

		protected internal override void Faulted(CoreProjectionStatusMessage.Faulted message) {
			SetFaulted(message.FaultedReason);
			_managedProjection.PersistedProjectionState.SourceDefinition = null;
			_managedProjection.WriteStartOrLoadStopped();
		}

		protected internal override void Prepared(CoreProjectionStatusMessage.Prepared message) {
			_managedProjection.SetState(ManagedProjectionState.Prepared);
			_managedProjection.PersistedProjectionState.SourceDefinition = message.SourceDefinition;
			_managedProjection.Prepared = true;
			_managedProjection.Created = true;
			_managedProjection.WriteStartOrLoadStopped();
		}
	}
}
