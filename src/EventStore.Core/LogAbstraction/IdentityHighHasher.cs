// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.Index.Hashes;

namespace EventStore.Core.LogAbstraction {
	// if the streams are only 32 bit, neednt have 64 bits in the index
	public class IdentityHighHasher : IHasher<uint> {
		public uint Hash(uint s) => 0;
	}
}
