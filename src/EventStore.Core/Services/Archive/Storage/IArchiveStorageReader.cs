// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;
using System.Threading;

namespace EventStore.Core.Services.Archive.Storage;

public interface IArchiveStorageReader {
	/// <summary>List all chunk files present in the archive</summary>
	/// <returns>The file names of all chunks present in the archive</returns>
	public IAsyncEnumerable<string> ListChunks(CancellationToken ct);
}
