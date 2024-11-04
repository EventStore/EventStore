// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading;
using System.Threading.Tasks;
using DotNext;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog.Scavenging;

public class ChunkReaderForIndexExecutor<TStreamId> : IChunkReaderForIndexExecutor<TStreamId> {
	private readonly Func<TFReaderLease> _tfReaderFactory;

	public ChunkReaderForIndexExecutor(Func<TFReaderLease> tfReaderFactory) {
		_tfReaderFactory = tfReaderFactory;
	}

	public async ValueTask<Optional<TStreamId>> TryGetStreamId(long position, CancellationToken token) {
		using var reader = _tfReaderFactory();
		var result = await reader.TryReadAt(position, couldBeScavenged: true, token);
		if (!result.Success) {
			return Optional.None<TStreamId>();
		}

		if (result.LogRecord is not IPrepareLogRecord<TStreamId> prepare)
			throw new Exception($"Record in index at position {position} is not a prepare");

		return prepare.EventStreamId;
	}
}
