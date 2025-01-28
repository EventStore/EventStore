// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.TransactionLog.Scavenging.Interfaces;
using EventStore.Core.Transforms;
using Serilog;

namespace EventStore.Core.TransactionLog.Scavenging.DbAccess;

public class ChunkWriterForExecutor<TStreamId> : IChunkWriterForExecutor<TStreamId, ILogRecord> {
	private readonly ILogger _logger;
	private readonly ChunkManagerForExecutor<TStreamId> _manager;
	private readonly TFChunk _outputChunk;
	private readonly ScavengedChunkWriter _scavengedChunkWriter;

	private ChunkWriterForExecutor(
		ILogger logger,
		ChunkManagerForExecutor<TStreamId> manager,
		TFChunk outputChunk) {

		_logger = logger;
		_manager = manager;
		_outputChunk = outputChunk;
		_scavengedChunkWriter = new ScavengedChunkWriter(outputChunk);
	}

	public static async ValueTask<ChunkWriterForExecutor<TStreamId>> CreateAsync(
		ILogger logger,
		ChunkManagerForExecutor<TStreamId> manager,
		TFChunkDbConfig dbConfig,
		IChunkReaderForExecutor<TStreamId, ILogRecord> sourceChunk,
		DbTransformManager transformManager,
		CancellationToken token) {

		// from TFChunkScavenger.ScavengeChunk
		var chunk = await TFChunk.CreateNew(
			manager.FileSystem,
			filename: Path.Combine(dbConfig.Path, Guid.NewGuid() + ".scavenge.tmp"),
			chunkDataSize: dbConfig.ChunkSize,
			chunkStartNumber: sourceChunk.ChunkStartNumber,
			chunkEndNumber: sourceChunk.ChunkEndNumber,
			isScavenged: true,
			inMem: dbConfig.InMemDb,
			unbuffered: dbConfig.Unbuffered,
			writethrough: dbConfig.WriteThrough,
			reduceFileCachePressure: dbConfig.ReduceFileCachePressure,
			tracker: new TFChunkTracker.NoOp(),
			getTransformFactory: transformManager,
			token);

		return new(logger, manager, chunk);
	}

	public string LocalFileName => _outputChunk.LocalFileName;

	public ValueTask WriteRecord(RecordForExecutor<TStreamId, ILogRecord> record, CancellationToken token) =>
		_scavengedChunkWriter.WriteRecord(record.Record, token);

	public async ValueTask<(string NewFileName, long NewFileSize)> Complete(CancellationToken token) {
		await _scavengedChunkWriter.Complete(token);

		var newFileName = await _manager.SwitchInTempChunk(chunk: _outputChunk, token);
		return (newFileName, _outputChunk.FileSize);
	}

	// tbh not sure why this distinction is important
	public void Abort(bool deleteImmediately) {
		if (deleteImmediately) {
			_outputChunk.Dispose();
			TFChunkScavenger<TStreamId>.DeleteTempChunk(_logger, LocalFileName, TFChunkScavenger.MaxRetryCount);
		} else {
			_outputChunk.MarkForDeletion();
		}
	}
}
