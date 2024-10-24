// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Tests.Services.Storage;

public class FakeIndexWriter<TStreamId> : IIndexWriter<TStreamId> {
	public long CachedTransInfo => 0;
	public long NotCachedTransInfo => 0;
	public void Reset() { }

	private TStreamId GetFakeStreamId() {
		if (typeof(TStreamId) == typeof(long)) {
			return (TStreamId) (object) 0L;
		}

		if (typeof(TStreamId) == typeof(string)) {
			return (TStreamId) (object) string.Empty;
		}

		throw new NotSupportedException();
	}

	public ValueTask<CommitCheckResult<TStreamId>> CheckCommitStartingAt(long transactionPosition, long commitPosition, CancellationToken token) {
		return ValueTask.FromResult(new CommitCheckResult<TStreamId>(CommitDecision.Ok, GetFakeStreamId(), -1, -1, -1, false));
	}

	public ValueTask<CommitCheckResult<TStreamId>> CheckCommit(TStreamId streamId, long expectedVersion, IAsyncEnumerable<Guid> eventIds, bool streamMightExist, CancellationToken token) {
		return ValueTask.FromResult(new CommitCheckResult<TStreamId>(CommitDecision.Ok, streamId, expectedVersion, -1, -1, false));
	}

	public ValueTask PreCommit(CommitLogRecord commit, CancellationToken token)
		=> token.IsCancellationRequested ? ValueTask.FromCanceled(token) : ValueTask.CompletedTask;

	public void PreCommit(ReadOnlySpan<IPrepareLogRecord<TStreamId>> committedPrepares) { }

	public void UpdateTransactionInfo(long transactionId, long logPosition, TransactionInfo<TStreamId> transactionInfo) { }

	public ValueTask<TransactionInfo<TStreamId>> GetTransactionInfo(long writerCheckpoint, long transactionId,
		CancellationToken token)
		=> ValueTask.FromResult<TransactionInfo<TStreamId>>(default);

	public void PurgeNotProcessedCommitsTill(long checkpoint) { }

	public void PurgeNotProcessedTransactions(long checkpoint) { }

	public ValueTask<bool> IsSoftDeleted(TStreamId streamId, CancellationToken token) => new(false);

	public ValueTask<long> GetStreamLastEventNumber(TStreamId streamId, CancellationToken token) => new(-1L);

	public ValueTask<StreamMetadata> GetStreamMetadata(TStreamId streamId, CancellationToken token) =>
		new(StreamMetadata.Empty);

	public ValueTask<RawMetaInfo> GetStreamRawMeta(TStreamId streamId, CancellationToken token)
		=> ValueTask.FromResult(new RawMetaInfo());

	public TStreamId GetStreamId(string streamName) => GetFakeStreamId();

	public ValueTask<string> GetStreamName(TStreamId streamId, CancellationToken token) => new(string.Empty);
}
