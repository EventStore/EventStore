// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.Messaging;

namespace EventStore.Core.Tests.Bus.Helpers {
	public class DeferredExecutionTestMessage : Message {
		private readonly Action _action;

		public DeferredExecutionTestMessage(Action action) {
			if (action == null)
				throw new ArgumentNullException("action");
			_action = action;
		}

		public void Execute() {
			_action();
		}
	}

	public class ExecutableTestMessage : Message {
		private readonly Action _action;

		public ExecutableTestMessage(Action action) {
			if (action == null)
				throw new ArgumentNullException("action");
			_action = action;
		}

		public void Execute() {
			_action();
		}
	}
}
