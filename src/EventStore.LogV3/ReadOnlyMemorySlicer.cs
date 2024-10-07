// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.LogV3;

public struct ReadOnlyMemorySlicer<T> {
	public ReadOnlyMemory<T> Remaining { get; private set; }

	public ReadOnlyMemorySlicer(ReadOnlyMemory<T> source) {
		Remaining = source;
	}

	public ReadOnlyMemory<T> Slice(int length) {
		var toReturn = Remaining[..length];
		Remaining = Remaining[length..];
		return toReturn;
	}
}
