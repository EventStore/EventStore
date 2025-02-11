// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.POC.IO.Core;

namespace EventStore.Core.Subsystems;

public class InternalOperationsClient : IOperationsClient {
	readonly IPublisher _publisher;

	public InternalOperationsClient(IPublisher publisher) {
		_publisher = publisher;
	}

	public void Resign() {
		_publisher.Publish(new ClientMessage.ResignNode());
	}
}
