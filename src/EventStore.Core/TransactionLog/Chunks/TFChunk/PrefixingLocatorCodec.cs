// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.TransactionLog.Chunks.TFChunk;

// Prefixes remote chunks. Leaves local chunks as-is
public class PrefixingLocatorCodec : ILocatorCodec {
	const string RemotePrefix = "archive:";

	private readonly int _remotePrefixLength = RemotePrefix.Length;

	public string EncodeLocalName(string fileName) => fileName;

	public string EncodeRemoteName(string objectName) => $"{RemotePrefix}{objectName}";

	public bool Decode(string locator, out string decoded) {
		if (locator.StartsWith(RemotePrefix)) {
			// remote
			decoded = locator[_remotePrefixLength..];
			return true;
		}

		// local
		decoded = locator;
		return false;
	}
}
