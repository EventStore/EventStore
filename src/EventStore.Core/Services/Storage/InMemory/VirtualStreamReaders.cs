// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.Services.Storage.InMemory;

public class VirtualStreamReaders(IVirtualStreamReader[] readers) {
	public bool TryGetReader(string streamId, out IVirtualStreamReader reader) {
		for (var i = 0; i < readers.Length; i++) {
			if (!readers[i].OwnStream(streamId)) continue;
			reader = readers[i];
			return true;
		}

		reader = null;
		return false;
	}
}
