// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading;

namespace EventStore.Core.Services.Storage.InMemory;

// does not support $all style reads, but tracks the LastCommitPosition so that
// the long poll mechanism works in the SubscriptionsService.
// note that the SubscriptionsService currently only supports one physical log
// and one inmemory log so we can't have separate inmemory logs per inmemory stream.
public class InMemoryLog {
	long _lastCommitPosition;

	public long GetLastCommitPosition() => Interlocked.Read(ref _lastCommitPosition);
	public long GetNextCommitPosition() => Interlocked.Increment(ref _lastCommitPosition);
}
