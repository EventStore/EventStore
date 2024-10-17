// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading.Tasks;

namespace EventStore.Core.Services.Archiver.Storage;

public interface IArchiveStorage {
	public static IArchiveStorage None = new NoArchiveStorage();
	public ValueTask StoreChunk(string path);
}

file class NoArchiveStorage : IArchiveStorage {
	public ValueTask StoreChunk(string path) {
		throw new InvalidOperationException();
	}
}
