// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.POC.ConnectorsEngine.Infrastructure;

public abstract class StateMachine {
	private readonly Dispatcher _dispatcher = new();

	protected void Register<T>(Action<T> handler) where T : Message {
		_dispatcher.Register(handler);
	}

	public void Handle(Message msg) {
		_dispatcher.Dispatch(msg);
	}
}
