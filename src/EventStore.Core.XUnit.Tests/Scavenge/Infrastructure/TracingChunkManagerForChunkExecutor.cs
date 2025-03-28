// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using EventStore.Core.TransactionLog.Scavenging.Interfaces;

namespace EventStore.Core.XUnit.Tests.Scavenge.Infrastructure;

public class TracingChunkManagerForChunkExecutor<TStreamId, TRecord> :
	IChunkManagerForChunkExecutor<TStreamId, TRecord> {

	private readonly IChunkManagerForChunkExecutor<TStreamId, TRecord> _wrapped;
	private readonly HashSet<int> _remoteChunks;
	private readonly Tracer _tracer;

	public TracingChunkManagerForChunkExecutor(
		IChunkManagerForChunkExecutor<TStreamId, TRecord> wrapped,
		HashSet<int> remoteChunks,
		Tracer tracer) {

		_wrapped = wrapped;
		_remoteChunks = remoteChunks;
		_tracer = tracer;
	}

	public IChunkFileSystem FileSystem => _wrapped.FileSystem;

	public async ValueTask<IChunkWriterForExecutor<TStreamId, TRecord>> CreateChunkWriter(
		IChunkReaderForExecutor<TStreamId, TRecord> sourceChunk,
		CancellationToken token) {

		return new TracingChunkWriterForExecutor<TStreamId, TRecord>(
			await _wrapped.CreateChunkWriter(sourceChunk, token),
			_tracer);
	}

	public IChunkReaderForExecutor<TStreamId, TRecord> GetChunkReaderFor(long position) {
		var reader = _wrapped.GetChunkReaderFor(position);
		var isRemote = _remoteChunks.Contains(reader.ChunkStartNumber);
		return new TrackingChunkReaderForExecutor<TStreamId, TRecord>(reader, isRemote, _tracer);
	}
}
