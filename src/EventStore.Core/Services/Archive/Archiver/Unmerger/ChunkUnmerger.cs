// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;

namespace EventStore.Core.Services.Archive.Archiver.Unmerger;

public class ChunkUnmerger : IChunkUnmerger {
	public IAsyncEnumerable<string> Unmerge(string chunkPath, int chunkStartNumber, int chunkEndNumber) {
		throw new NotImplementedException();
	}
}
