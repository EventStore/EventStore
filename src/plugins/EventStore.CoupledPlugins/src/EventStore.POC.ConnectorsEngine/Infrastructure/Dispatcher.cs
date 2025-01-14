// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;

namespace EventStore.POC.ConnectorsEngine.Infrastructure;

public class Dispatcher {
	private readonly Dictionary<Type, Action<Message>> _handlers = new();

	public void Register<T>(Action<T> handler) where T : Message {
		_handlers.Add(typeof(T), msg => handler((T)msg));
	}

	public void Dispatch(Message msg) {
		if (!_handlers.TryGetValue(msg.GetType(), out var handler))
			return;

		handler(msg);
	}
}
