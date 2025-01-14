// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.POC.ConnectorsEngine.Processing;

namespace Eventstore.POC.Tests.Processing;

sealed class AdHocConnector : IConnector {
	private readonly Action _onRun;
	private readonly Action _onDispose;

	public AdHocConnector(Action onRun, Action onDispose) {
		_onRun = onRun;
		_onDispose = onDispose;
	}

	public Task RunAsync() {
		_onRun();
		return Task.CompletedTask;
	}

	public void Dispose() {
		_onDispose();
	}
}
