// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace EventStore.Core.TransactionLog.Scavenging.Stages;

public class ChunkDeleter<TStreamId, TRecord> : IChunkDeleter<TStreamId, TRecord> {
	private readonly ILogger _logger;
	private readonly AdvancingCheckpoint _archiveCheckpoint;
	private readonly TimeSpan _retainPeriod;
	private readonly long _retainBytes;
	private readonly int _maxAttempts;
	private readonly TimeSpan _retryDelay;

	public ChunkDeleter(
		ILogger logger,
		AdvancingCheckpoint archiveCheckpoint,
		TimeSpan retainPeriod,
		long retainBytes,
		int maxAttempts = 10,
		int retryDelayMs = 1000) {

		_logger = logger;
		_archiveCheckpoint = archiveCheckpoint;
		_retainPeriod = retainPeriod;
		_retainBytes = retainBytes;
		_maxAttempts = maxAttempts;
		_retryDelay = TimeSpan.FromMilliseconds(retryDelayMs);

		_logger.Debug("SCAVENGING: Chunk retention criteria is Days: {Days}, LogicalBytes: {LogicalBytes}",
			retainPeriod.Days,
			retainBytes);
	}

	// returns true iff deleted
	public async ValueTask<bool> DeleteIfNotRetained(
		ScavengePoint scavengePoint,
		IScavengeStateForChunkExecutorWorker<TStreamId> concurrentState,
		IChunkReaderForExecutor<TStreamId, TRecord> physicalChunk,
		CancellationToken ct) {

		if (!ShouldDeleteForBytes(scavengePoint, physicalChunk)) {
			return false;
		}

		if (!ShouldDeleteForPeriod(scavengePoint, concurrentState, physicalChunk)) {
			return false;
		}

		await EnsurePresentInArchive(physicalChunk, ct);
		await DeletePhysicalChunk(physicalChunk, ct);
		return true;
	}

	// ideally we want the scavenge to have the same results when run on
	// different nodes so we do not skip over deleting this chunk if it is not in
	// the archive, we stop the scavenge instead.
	private async ValueTask EnsurePresentInArchive(
		IChunkReaderForExecutor<TStreamId, TRecord> physicalChunk,
		CancellationToken ct) {

		var logicalChunkNumber = physicalChunk.ChunkEndNumber;
		var lastError = default(Exception);

		for (var attempt = 0; attempt < _maxAttempts; attempt++) {
			if (attempt != 0)
				await Task.Delay(_retryDelay, ct);

			try {
				var isPresent = await _archiveCheckpoint.IsGreaterThanOrEqualTo(physicalChunk.ChunkEndPosition, ct);
				if (isPresent) {
					return;
				} else {
					lastError = new Exception($"Chunk {logicalChunkNumber} is not yet present in the archive. Check that the Archiver node is functioning correctly and re-run the scavenge to continue.");
					_logger.Warning("Logical chunk {LogicalChunkNumber} is not yet present in the archive. Attempt {Attempt}/{MaxAttempts}",
						logicalChunkNumber, attempt + 1, _maxAttempts);
				}
			} catch (Exception ex) {
				lastError = ex;
				_logger.Warning(ex, "Unable to determine existence of logical chunk {LogicalChunkNumber} in the archive. Attempt {Attempt}/{MaxAttempts}",
					logicalChunkNumber, attempt + 1, _maxAttempts);
			}
		}

		throw lastError ?? new Exception("unknown error");
	}

	private bool ShouldDeleteForBytes(
		ScavengePoint scavengePoint,
		IChunkReaderForExecutor<TStreamId, TRecord> physicalChunk) {

		var deleteBytesBefore = scavengePoint.Position - _retainBytes;
		return physicalChunk.ChunkEndPosition < deleteBytesBefore;
	}

	private bool ShouldDeleteForPeriod(
		ScavengePoint scavengePoint,
		IScavengeStateForChunkExecutorWorker<TStreamId> concurrentState,
		IChunkReaderForExecutor<TStreamId, TRecord> physicalChunk) {

		for (var logicalChunkNumber = physicalChunk.ChunkEndNumber;
			logicalChunkNumber >= physicalChunk.ChunkStartNumber;
			logicalChunkNumber--) {

			if (concurrentState.TryGetChunkTimeStampRange(logicalChunkNumber, out var createdAtRange)) {
				var deleteBefore = scavengePoint.EffectiveNow - _retainPeriod;
				return createdAtRange.Max < deleteBefore;
			} else {
				// we don't have a time stamp range for this logical chunk, it had no prepares in during
				// accumulation. we try an earlier logical chunk in this physical chunk.
			}
		}

		// no time stamp for any logical chunk in this physical chunk. its possible to get here if the
		// physical chunk doesn't have any prepares in it at all. it's fine to delete it.
		return true;
	}

	private ValueTask DeletePhysicalChunk(
		IChunkReaderForExecutor<TStreamId, TRecord> physicalChunk,
		CancellationToken ct) {

		// todo: actually delete the file in cooperation with the chunk manager
		// so that readers are allowed to complete and chunk manager knows that
		// the chunk has gone and must direct readers to the archive
		_logger.Debug(
			"SCAVENGING: Deleting physical chunk: {oldChunkName} " +
			"{chunkStartNumber} => {chunkEndNumber} ({chunkStartPosition} => {chunkEndPosition})",
			physicalChunk.Name,
			physicalChunk.ChunkStartNumber, physicalChunk.ChunkEndNumber,
			physicalChunk.ChunkStartPosition, physicalChunk.ChunkEndPosition);

		return ct.IsCancellationRequested
			? ValueTask.FromCanceled(ct)
			: ValueTask.CompletedTask;
	}
}
