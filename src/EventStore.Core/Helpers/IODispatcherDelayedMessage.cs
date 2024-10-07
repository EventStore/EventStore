// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.Messaging;

namespace EventStore.Core.Helpers;

[DerivedMessage(CoreMessage.IODispatcher)]
public sealed partial class IODispatcherDelayedMessage : Message {
	private readonly Guid _correlationId;
	private readonly ICorrelatedTimeout _timeout;
	private readonly Guid? _messageCorrelationId;

	public IODispatcherDelayedMessage(Guid correlationId, ICorrelatedTimeout timeout) {
		_timeout = timeout;
		_correlationId = correlationId;
	}

	public IODispatcherDelayedMessage(Guid correlationId, ICorrelatedTimeout timeout, Guid messageCorrelationId) {
		_timeout = timeout;
		_correlationId = correlationId;
		_messageCorrelationId = messageCorrelationId;
	}

	public void Timeout() {
		_timeout.Timeout(_messageCorrelationId ?? Guid.Empty);
	}

	public Guid CorrelationId {
		get { return _correlationId; }
	}

	public Guid? MessageCorrelationId {
		get { return _messageCorrelationId; }
	}
}
