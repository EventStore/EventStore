// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.Index.Hashes;
using StreamId = System.UInt32;

namespace EventStore.Core.LogAbstraction;

public class IdentityLowHasher : IHasher<StreamId> {
	public uint Hash(StreamId s) => s;
}
