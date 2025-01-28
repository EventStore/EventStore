// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Resilience;
using EventStore.Core.Services.Archive.Archiver;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using Polly;
using Serilog;

namespace EventStore.Core.Services.Archive.Storage;

public class ResilientArchiveStorage(ResiliencePipeline pipeline, IArchiveStorage wrapped) : IArchiveStorage {
	private static readonly ILogger Log = Serilog.Log.ForContext<ArchiverService>();
	private readonly PipelineExecutor _executor = new(pipeline, nameof(ResilientArchiveStorage), Log);

	public ValueTask SetCheckpoint(long checkpoint, CancellationToken ct) =>
		_executor.ExecuteAsync(
			static (context, state) => 
				state.wrapped.SetCheckpoint(state.checkpoint, context.CancellationToken),
			(wrapped, checkpoint),
			ct);

	public ValueTask StoreChunk(IChunkBlob chunk, CancellationToken ct) =>
		_executor.ExecuteAsync(
			static (context, state) => state.wrapped.StoreChunk(state.chunk, context.CancellationToken),
			(wrapped, chunk),
			ct);

	public ValueTask<long> GetCheckpoint(CancellationToken ct) =>
		_executor.ExecuteAsync(
			static (context, wrapped) => wrapped.GetCheckpoint(context.CancellationToken),
			wrapped,
			ct);

	public ValueTask<int> ReadAsync(int logicalChunkNumber, Memory<byte> buffer, long offset, CancellationToken ct) =>
		_executor.ExecuteAsync(
			static (context, state) => state.wrapped.ReadAsync(state.logicalChunkNumber, state.buffer, state.offset, context.CancellationToken),
			(wrapped, logicalChunkNumber, buffer, offset),
			ct);

	public ValueTask<ArchivedChunkMetadata> GetMetadataAsync(int logicalChunkNumber, CancellationToken ct) =>
		_executor.ExecuteAsync(
			static (context, state) => state.wrapped.GetMetadataAsync(state.logicalChunkNumber, context.CancellationToken),
			(wrapped, logicalChunkNumber),
			ct);
}
