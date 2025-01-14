// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.AutoScavenge.Tests;

public class SimulatorCommands {
	public record FastForward(TimeSpan Time) : Command<Unit>(_ => { });

	public record Suspend() : Command<Unit>(_ => { });
}
