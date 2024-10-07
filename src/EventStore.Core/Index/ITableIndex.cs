// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Core.Index;

public interface ITableIndex {
	long CommitCheckpoint { get; }
	long PrepareCheckpoint { get; }

	void Initialize(long chaserCheckpoint);
	void Close(bool removeFiles = true);

	// this overload keeps IndexEntries that exist in the log
	void Scavenge(IIndexScavengerLog log, CancellationToken ct);
	// this overload keeps IndexEntries that pass the keep predicate
	void Scavenge(Func<IndexEntry, bool> shouldKeep, IIndexScavengerLog log, CancellationToken ct);
	Task MergeIndexes();
	IEnumerable<ISearchTable> IterateAllInOrder();
	bool IsBackgroundTaskRunning { get; }
}

public interface ITableIndex<TStreamId> : ITableIndex {
	void Add(long commitPos, TStreamId streamId, long version, long position);
	void AddEntries(long commitPos, IList<IndexKey<TStreamId>> entries);

	bool TryGetOneValue(TStreamId streamId, long version, out long position);
	bool TryGetLatestEntry(TStreamId streamId, out IndexEntry entry);
	bool TryGetLatestEntry(ulong stream, long beforePosition, Func<IndexEntry, bool> isForThisStream, out IndexEntry entry);
	bool TryGetLatestEntry(TStreamId streamId, long beforePosition, Func<IndexEntry, bool> isForThisStream, out IndexEntry entry);
	bool TryGetOldestEntry(TStreamId streamId, out IndexEntry entry);
	bool TryGetNextEntry(TStreamId streamId, long afterVersion, out IndexEntry entry);
	bool TryGetNextEntry(ulong stream, long afterVersion, out IndexEntry entry);
	bool TryGetPreviousEntry(TStreamId streamId, long beforeVersion, out IndexEntry entry);
	bool TryGetPreviousEntry(ulong stream, long beforeVersion, out IndexEntry entry);

	IReadOnlyList<IndexEntry> GetRange(TStreamId streamId, long startVersion, long endVersion, int? limit = null);
	IReadOnlyList<IndexEntry> GetRange(ulong stream, long startVersion, long endVersion, int? limit = null);

	void WaitForBackgroundTasks(int millisecondsTimeout);
}
