// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.Services.Transport.Common;

public readonly struct HashCode {
	private readonly int _value;

	private HashCode(int value) {
		_value = value;
	}

	public static readonly HashCode Hash = default;

	public HashCode Combine<T>(T value) where T: struct {
		unchecked {
			return new HashCode((_value * 397) ^ value.GetHashCode());
		}
	}

	public static implicit operator int(HashCode value) => value._value;
}
