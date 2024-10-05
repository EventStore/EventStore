// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Management.ManagedProjectionStates;

internal abstract class ManagedProjectionStateBase {
	protected readonly ManagedProjection _managedProjection;

	protected ManagedProjectionStateBase(ManagedProjection managedProjection) {
		_managedProjection = managedProjection;
	}

	private void Unexpected(string message) {
		_managedProjection.Fault(message + " in " + this.GetType().Name);
	}

	protected void SetFaulted(string reason) {
		_managedProjection.Fault(reason);
	}

	protected internal virtual void Started() {
		Unexpected("Unexpected 'STARTED' message");
	}

	protected internal virtual void Stopped(CoreProjectionStatusMessage.Stopped message) {
		Unexpected("Unexpected 'STOPPED' message");
	}

	protected internal virtual void Faulted(CoreProjectionStatusMessage.Faulted message) {
		Unexpected("Unexpected 'FAULTED' message");
	}

	protected internal virtual void Prepared(CoreProjectionStatusMessage.Prepared message) {
		Unexpected("Unexpected 'PREPARED' message");
	}
}
