// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.LogCommon {
	public enum LogRecordType : byte {
		Prepare = 0,
		Commit = 1,
		System = 2,
		PartitionType = 3,
		StreamType = 4,
		EventType = 5,
		ContentType = 6,
		Partition = 7,
		StreamWrite = 8,
		TransactionStart = 9,
		TransactionEnd = 10,
		Stream = 11,
	}
}
