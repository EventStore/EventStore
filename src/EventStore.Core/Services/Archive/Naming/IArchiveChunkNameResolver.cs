// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Core.Services.Archive.Naming;

public interface IArchiveChunkNameResolver {
	string Prefix { get; } // The prefix that is applied to all chunks
	ValueTask<string> GetFileNameFor(int logicalChunkNumber, CancellationToken token);
}
