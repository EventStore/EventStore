// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Diagnostics;
using EventStore.Common.Utils;

namespace EventStore.Core.Messaging;

public class CallbackEnvelope : IEnvelope {
	private readonly Action<Message> _callback;

	public CallbackEnvelope(Action<Message> callback) {
		Debug.Assert(callback != null);
		_callback = callback;
	}

	public void ReplyWith<T>(T message) where T : Message {
		_callback(message);
	}
}
