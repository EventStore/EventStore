// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.Bus;

namespace EventStore.Core.Messaging;

// USE ONLY WHEN YOU KNOW WHAT YOU ARE DOING
// - no support for async handlers
// - calls the handler directly on the replying thread
// - limited type safety
public class SendToThisEnvelope(object receiver) : IEnvelope {
	public void ReplyWith<T>(T message) where T : Message {
		switch (receiver) {
			case IHandle<T> handle:
				handle.Handle(message);
				break;
			case IAsyncHandle<T>:
				throw new Exception($"SendToThisEnvelope does not support asynchronous receivers. Receiver: {receiver}");
		}
	}
}
