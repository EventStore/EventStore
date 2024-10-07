// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;

namespace EventStore.Core.Tests.Bus.Helpers;

public class WaitingConsumer : IHandle<Message>, IDisposable {
	public readonly List<Message> HandledMessages = new List<Message>();

	private readonly CountdownEvent _countdownEvent;

	public WaitingConsumer(int initialCount) {
		_countdownEvent = new CountdownEvent(initialCount);
	}

	public void SetWaitingCount(int count) {
		_countdownEvent.Reset(count);
	}

	public bool Wait(int ms = 5000) {
		return _countdownEvent.Wait(ms);
	}

	public void Handle(Message message) {
		HandledMessages.Add(message);

		var typedMsg = message as DeferredExecutionTestMessage;
		if (typedMsg != null)
			((Action<DeferredExecutionTestMessage>)(deffered => deffered.Execute()))(typedMsg);

		var executableTestMessage = message as ExecutableTestMessage;
		if (executableTestMessage != null)
			((Action<ExecutableTestMessage>)(deffered => deffered.Execute()))(executableTestMessage);

		_countdownEvent.Signal();
	}

	public void Dispose() {
		_countdownEvent.Dispose();
	}
}
