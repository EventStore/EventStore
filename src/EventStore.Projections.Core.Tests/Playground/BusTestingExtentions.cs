// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;

namespace EventStore.Projections.Core.Tests.Playground;

public static class BusTestingExtentions {
	private class Handler<T> : IHandle<T> where T : Message {
		private readonly Action<T> _handler;

		public Handler(Action<T> handler) {
			_handler = handler;
		}

		public void Handle(T message) {
			_handler(message);
		}
	}

	public static void Subscribe<T>(this ISubscriber self, Action<T> handler) where T : Message {
		self.Subscribe(new Handler<T>(handler));
	}
}
