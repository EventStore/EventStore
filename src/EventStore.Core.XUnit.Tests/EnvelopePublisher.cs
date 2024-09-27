// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.Bus;
using EventStore.Core.Messaging;

namespace EventStore.Core.XUnit.Tests;

class EnvelopePublisher : IPublisher {
	private readonly IEnvelope _envelope;

	public EnvelopePublisher(IEnvelope envelope) {
		_envelope = envelope;
	}

	public void Publish(Message message) {
		_envelope.ReplyWith(message);
	}
}
