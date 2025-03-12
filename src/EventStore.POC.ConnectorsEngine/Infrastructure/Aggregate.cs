// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.POC.ConnectorsEngine.Infrastructure;

public abstract class Aggregate {
	public abstract State BaseState { get; }

	public void Append(Message msg) {
		BaseState.Append(msg);
	}
}

public abstract class Aggregate<TState> : Aggregate
	where TState : State, new() {

	public override State BaseState => State;

	public TState State { get; } = new();
}
