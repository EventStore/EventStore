// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Core.Index.Hashes;

public interface IHasher {
	uint Hash(ReadOnlySpan<byte> data);
	uint Hash(byte[] data, int offset, uint len, uint ls);
}

public interface IHasher<in TStreamId> {
	uint Hash(TStreamId s);
}

public interface ILongHasher<T> {
	ulong Hash(T x);
}
