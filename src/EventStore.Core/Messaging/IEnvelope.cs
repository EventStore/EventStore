// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.Messaging;

public interface IEnvelope<in T> {
	void ReplyWith<U>(U message) where U : T;
}

public interface IEnvelope : IEnvelope<Message> {
	public static IEnvelope NoOp => NoOpEnvelope.Instance;
}

file sealed class NoOpEnvelope : IEnvelope {
	public static NoOpEnvelope Instance { get; } = new();

	public void ReplyWith<T>(T message) where T : Message { }
}
