// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.Time;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog;

public interface ITransactionFileTracker {
	void OnRead(Instant start, ILogRecord record, Source source);

	enum Source {
		Unknown,
		Archive,
		ChunkCache,
		FileSystem,
	};
}
