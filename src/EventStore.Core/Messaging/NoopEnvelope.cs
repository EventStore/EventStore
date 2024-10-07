// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.Messaging;

public class NoopEnvelope : IEnvelope {
	public void ReplyWith<T>(T message) where T : Message {
	}
}
