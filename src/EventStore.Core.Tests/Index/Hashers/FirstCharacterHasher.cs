// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.Index.Hashes;

namespace EventStore.Core.Tests.Index.Hashers;

public class FirstCharacterHasher : ILongHasher<string> {
	public ulong Hash(string x) =>
		(ulong)(x.Length == 0 ? 0 : x[0].GetHashCode());
}
