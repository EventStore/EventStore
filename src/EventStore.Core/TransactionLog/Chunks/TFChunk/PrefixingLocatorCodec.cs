// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.TransactionLog.Chunks.TFChunk;

// Prefixes remote chunks. Leaves local chunks as-is
public class PrefixingLocatorCodec : ILocatorCodec {
	const string RemotePrefix = "archived-chunk-";

	private readonly int _remotePrefixLength = RemotePrefix.Length;

	public string EncodeLocal(string fileName) => fileName;

	public string EncodeRemote(int chunkNumber) => $"{RemotePrefix}{chunkNumber}";

	public bool Decode(string locator, out int chunkNumber, out string fileName) {
		if (locator.StartsWith(RemotePrefix)) {
			// remote
			chunkNumber = int.Parse(locator[_remotePrefixLength..]);
			fileName = default;
			return true;
		}

		// local
		fileName = locator;
		chunkNumber = default;
		return false;
	}
}
