// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.Transforms;
using Serilog;

namespace EventStore.Core.TransactionLog.Scavenging;

public class ChunkWriterForExecutor<TStreamId> : IChunkWriterForExecutor<TStreamId, ILogRecord> {
	const int BatchLength = 2000;
	private readonly ILogger _logger;
	private readonly ChunkManagerForExecutor<TStreamId> _manager;
	private readonly TFChunk _outputChunk;
	private readonly List<List<PosMap>> _posMapss;
	private int _lastFlushedPage = -1;

	public ChunkWriterForExecutor(
		ILogger logger,
		ChunkManagerForExecutor<TStreamId> manager,
		TFChunkDbConfig dbConfig,
		IChunkReaderForExecutor<TStreamId, ILogRecord> sourceChunk,
		DbTransformManager transformManager) {

		_logger = logger;
		_manager = manager;

		// list of lists to avoid having an enormous list which could make it to the LoH
		// and to avoid expensive resize operations on large lists
		_posMapss = new List<List<PosMap>>(capacity: BatchLength) {
			new List<PosMap>(capacity: BatchLength)
		};

		// from TFChunkScavenger.ScavengeChunk
		FileName = Path.Combine(dbConfig.Path, Guid.NewGuid() + ".scavenge.tmp");
		_outputChunk = TFChunk.CreateNew(
			filename: FileName,
			chunkDataSize: dbConfig.ChunkSize,
			chunkStartNumber: sourceChunk.ChunkStartNumber,
			chunkEndNumber: sourceChunk.ChunkEndNumber,
			isScavenged: true,
			inMem: dbConfig.InMemDb,
			unbuffered: dbConfig.Unbuffered,
			writethrough: dbConfig.WriteThrough,
			reduceFileCachePressure: dbConfig.ReduceFileCachePressure,
			tracker: new TFChunkTracker.NoOp(),
			transformFactory: transformManager.GetFactoryForNewChunk());
	}

	public string FileName { get; }

	public void WriteRecord(RecordForExecutor<TStreamId, ILogRecord> record) {
		var posMap = TFChunkScavenger<TStreamId>.WriteRecord(_outputChunk, record.Record);

		// add the posmap in memory so we can write it when we complete
		var lastBatch = _posMapss[_posMapss.Count - 1];
		if (lastBatch.Count >= BatchLength) {
			lastBatch = new List<PosMap>(capacity: BatchLength);
			_posMapss.Add(lastBatch);
		}

		lastBatch.Add(posMap);

		// occasionally flush the chunk. based on TFChunkScavenger.ScavengeChunk
		var currentPage = _outputChunk.RawWriterPosition / 4046;
		if (currentPage - _lastFlushedPage > TFChunkScavenger.FlushPageInterval) {
			_outputChunk.Flush();
			_lastFlushedPage = currentPage;
		}
	}

	public async ValueTask<(string, long)> Complete(CancellationToken token) {
		// write posmap
		var posMapCount = 0;
		foreach (var list in _posMapss)
			posMapCount += list.Count;

		var unifiedPosMap = new List<PosMap>(capacity: posMapCount);
		foreach (var list in _posMapss)
			unifiedPosMap.AddRange(list);

		await _outputChunk.CompleteScavenge(unifiedPosMap, token);
		_manager.SwitchChunk(chunk: _outputChunk, out var newFileName);

		return (newFileName, _outputChunk.FileSize);
	}

	// tbh not sure why this distinction is important
	public void Abort(bool deleteImmediately) {
		if (deleteImmediately) {
			_outputChunk.Dispose();
			TFChunkScavenger<TStreamId>.DeleteTempChunk(_logger, FileName, TFChunkScavenger.MaxRetryCount);
		} else {
			_outputChunk.MarkForDeletion();
		}
	}
}
