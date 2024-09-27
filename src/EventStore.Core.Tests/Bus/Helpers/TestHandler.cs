// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;

namespace EventStore.Core.Tests.Bus.Helpers {
	public class TestHandler<T> : IHandle<T> where T : Message {
		public readonly List<T> HandledMessages = new List<T>();

		public void Handle(T message) {
			HandledMessages.Add(message);
		}
	}
}
