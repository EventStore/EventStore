// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.POC.IO.Core;

namespace EventStore.POC.ConnectorsEngine.Processing.Checkpointing;

public readonly record struct ResetPoint(ulong Round, Position? Checkpoint) {
	public static bool operator <(ResetPoint a, ResetPoint b) {
		if (a.Round != b.Round)
			return a.Round < b.Round;
		
		if (a.Checkpoint is null)
			return b.Checkpoint is not null;

		if (b.Checkpoint is null)
			return false;

		return a.Checkpoint.Value < b.Checkpoint.Value;
	}

	public static bool operator >(ResetPoint a, ResetPoint b) => b < a;
}
