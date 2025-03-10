// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;

namespace EventStore.POC.ConnectorsEngine.Infrastructure;

public class State {
	private readonly List<Message> _pending = new();
	private readonly Dispatcher _dispatcher = new();

	public string Id { get; internal set; } = "";

	protected void Register<T>(Action<T> handler) where T : Message {
		_dispatcher.Register(handler);
	}

	public void Append(Message msg) {
		Apply(msg);
		_pending.Add(msg);
	}

	public void Apply(Message msg) {
		_dispatcher.Dispatch(msg);
	}

	public IReadOnlyList<Message> Pending => _pending;
}
