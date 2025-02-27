// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using System.Collections.Generic;
using System.Linq;
using EventStore.Common.Utils;
using EventStore.Core.Data;

namespace EventStore.Core.Services.Storage.ReaderIndex;

public struct IndexReadAllResult {
	public readonly List<CommitEventRecord> Records;
	public readonly TFPos CurrentPos;
	public readonly TFPos NextPos;
	public readonly TFPos PrevPos;
	public readonly bool IsEndOfStream;
	public readonly long ConsideredEventsCount;

	public IndexReadAllResult(List<CommitEventRecord> records, TFPos currentPos, TFPos nextPos, TFPos prevPos,
		bool isEndOfStream, long consideredEventsCount) {
		Ensure.NotNull(records, "records");

		Records = records;
		CurrentPos = currentPos;
		NextPos = nextPos;
		PrevPos = prevPos;
		IsEndOfStream = isEndOfStream;
		ConsideredEventsCount = consideredEventsCount;
	}

	public override string ToString() {
		return string.Format("CurrentPos: {0}, NextPos: {1}, PrevPos: {2}, IsEndOfStream: {3}, Records: {4}",
			CurrentPos, NextPos, PrevPos, string.Join("\n", IsEndOfStream, Records.Select(x => x.ToString())));
	}
}
