// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Security.EncryptionAtRest;

public readonly record struct MasterKey {
	public ushort Id { get; }
	public ReadOnlyMemory<byte> Key { get; }

	public MasterKey(int id, ReadOnlyMemory<byte> key) {
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(id, ushort.MaxValue);

		Id = (ushort) id;
		Key = key;
	}
}
