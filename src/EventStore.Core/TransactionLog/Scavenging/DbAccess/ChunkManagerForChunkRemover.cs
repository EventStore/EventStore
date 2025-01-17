// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Scavenging.Interfaces;

namespace EventStore.Core.TransactionLog.Scavenging.DbAccess;

public class ChunkManagerForChunkRemover : IChunkManagerForChunkRemover {
	private readonly TFChunkManager _manager;

	public ChunkManagerForChunkRemover(TFChunkManager manager) {
		_manager = manager;
	}

	public ValueTask<bool> SwitchInChunks(IReadOnlyList<string> locators, CancellationToken token) =>
		_manager.SwitchInCompletedChunks(locators, token);
}
