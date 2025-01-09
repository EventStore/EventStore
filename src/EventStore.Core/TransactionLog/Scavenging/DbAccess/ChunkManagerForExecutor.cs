// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.TransactionLog.Scavenging.Interfaces;
using EventStore.Core.Transforms;
using Serilog;

namespace EventStore.Core.TransactionLog.Scavenging.DbAccess;

public class ChunkManagerForExecutor<TStreamId> : IChunkManagerForChunkExecutor<TStreamId, ILogRecord> {
	private readonly ILogger _logger;
	private readonly TFChunkManager _manager;
	private readonly TFChunkDbConfig _dbConfig;
	private readonly DbTransformManager _transformManager;

	public ChunkManagerForExecutor(ILogger logger, TFChunkManager manager, TFChunkDbConfig dbConfig, DbTransformManager transformManager) {
		_logger = logger;
		_manager = manager;
		_dbConfig = dbConfig;
		_transformManager = transformManager;
	}

	public async ValueTask<IChunkWriterForExecutor<TStreamId, ILogRecord>> CreateChunkWriter(
		IChunkReaderForExecutor<TStreamId, ILogRecord> sourceChunk,
		CancellationToken token)
		=> await ChunkWriterForExecutor<TStreamId>.CreateAsync(_logger, this, _dbConfig, sourceChunk,
			_transformManager, token);

	public IChunkReaderForExecutor<TStreamId, ILogRecord> GetChunkReaderFor(long position) {
		var tfChunk = _manager.GetChunkFor(position);
		return new ChunkReaderForExecutor<TStreamId>(tfChunk);
	}

	public async ValueTask<string> SwitchChunk(
		TFChunk chunk,
		CancellationToken token) {

		var tfChunk = await _manager.SwitchChunk(
			chunk: chunk,
			verifyHash: false,
			removeChunksWithGreaterNumbers: false,
			token);

		if (tfChunk is null) {
			throw new Exception("Unexpected error: new chunk is null after switch");
		}

		return tfChunk.ChunkLocator;
	}
}
