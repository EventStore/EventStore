// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.Services.Archive.Archiver;

public static class Commands {
	public record ArchiveChunk : Command {
		public string ChunkPath;
		public int ChunkStartNumber;
		public int ChunkEndNumber;
		public long ChunkEndPosition;
	}
}
