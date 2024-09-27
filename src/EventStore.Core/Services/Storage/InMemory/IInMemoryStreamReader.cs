// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.Messages;

namespace EventStore.Core.Services.Storage.InMemory;

public interface IInMemoryStreamReader {
	ClientMessage.ReadStreamEventsForwardCompleted ReadForwards(ClientMessage.ReadStreamEventsForward msg);
	ClientMessage.ReadStreamEventsBackwardCompleted ReadBackwards(ClientMessage.ReadStreamEventsBackward msg);
}
