// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

#nullable enable

using System.Collections.Generic;

namespace EventStore.Core.Services.Archiver;

// prioritizes earlier chunks
public class ChunkPrioritizer : IComparer<Commands.ArchiveChunk> {
	public int Compare(Commands.ArchiveChunk? x, Commands.ArchiveChunk? y) {
		int cmp = x!.ChunkStartNumber.CompareTo(y!.ChunkStartNumber);

		if (cmp != 0)
			return cmp;

		cmp = x!.ChunkEndNumber.CompareTo(y!.ChunkEndNumber);
		return cmp;
	}
}
