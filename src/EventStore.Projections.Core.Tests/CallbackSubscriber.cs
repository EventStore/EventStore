// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;

namespace EventStore.Projections.Core.Tests {
	public static class CallbackSubscriber {
		class Impl<T> : IHandle<T> where T : Message {
			private readonly Action<T> _callback;

			public Impl(Action<T> callback) {
				_callback = callback;
			}

			public void Handle(T message) {
				_callback(message);
			}
		}

		public static IHandle<T> Create<T>(Action<T> callback) where T : Message {
			return new Impl<T>(callback);
		}
	}
}
