// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Core.Time;

public interface IClock {
	Instant Now { get; }
	long SecondsSinceEpoch { get; }
}

public class Clock : IClock {
	public static Clock Instance { get; } = new();
	private Clock() { }
	public Instant Now => Instant.Now;
	public long SecondsSinceEpoch => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}
