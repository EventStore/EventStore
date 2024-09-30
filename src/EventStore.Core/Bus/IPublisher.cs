// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.Messaging;

namespace EventStore.Core.Bus;

public interface IPublisher : IHandle<Message>, IEnvelope {
	void Publish(Message message);

	void IHandle<Message>.Handle(Message message) => Publish(message);

	void IEnvelope<Message>.ReplyWith<T>(T message) => Publish(message);
}
