// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.Services.Archive.Naming;

public interface IArchiveChunkNamer {
	// The prefix that is applied to all chunks
	string Prefix { get; }

	string GetFileNameFor(int logicalChunkNumber);
}
