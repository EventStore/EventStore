// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading;
using System.Threading.Tasks;
using static EventStore.Core.Messages.ClientMessage;

namespace EventStore.Core.Services.Storage.InMemory;

public interface IVirtualStreamReader {
	ValueTask<ReadStreamEventsForwardCompleted> ReadForwards(ReadStreamEventsForward msg, CancellationToken token);
	ValueTask<ReadStreamEventsBackwardCompleted> ReadBackwards(ReadStreamEventsBackward msg, CancellationToken token);
	ValueTask<long> GetLastEventNumber(string streamId);
	ValueTask<long> GetLastIndexedPosition();
	bool OwnStream(string streamId);
}
