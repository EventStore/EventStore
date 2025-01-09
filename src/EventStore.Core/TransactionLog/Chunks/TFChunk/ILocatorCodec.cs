// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.TransactionLog.Chunks.TFChunk;

// This is intended to be used within IChunkFileSystem only, to avoid leaking details of which
// chunks are remote and which are local.
public interface ILocatorCodec {
	string EncodeLocalName(string fileName);
	string EncodeRemoteName(string objectName);

	// returns false for local and true for remote
	bool Decode(string locator, out string decoded);
}
