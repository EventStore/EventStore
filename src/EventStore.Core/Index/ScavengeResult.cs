// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.Index;

public class ScavengeResult {
	public readonly IndexMap ScavengedMap;
	public readonly bool IsSuccess;
	public readonly PTable OldTable;
	public readonly PTable NewTable;
	public readonly long SpaceSaved;
	public readonly int Level;
	public readonly int Index;

	private ScavengeResult(IndexMap scavengedMap, bool isSuccess, PTable oldTable, PTable newTable, long spaceSaved,
		int level, int index) {
		ScavengedMap = scavengedMap;
		IsSuccess = isSuccess;
		OldTable = oldTable;
		NewTable = newTable;
		SpaceSaved = spaceSaved;
		Level = level;
		Index = index;
	}

	public static ScavengeResult Success(IndexMap scavengedMap, PTable oldTable, PTable newTable, long spaceSaved,
		int level, int index) {
		return new ScavengeResult(scavengedMap, true, oldTable, newTable, spaceSaved, level, index);
	}

	public static ScavengeResult Failed(PTable oldTable, int level, int index) {
		return new ScavengeResult(null, false, oldTable, null, 0, level, index);
	}
}
