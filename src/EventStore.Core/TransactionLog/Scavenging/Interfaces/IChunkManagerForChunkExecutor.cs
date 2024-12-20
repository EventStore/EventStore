// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Core.TransactionLog.Scavenging.Interfaces;

public interface IChunkManagerForChunkExecutor<TStreamId, TRecord> {
	ValueTask<IChunkWriterForExecutor<TStreamId, TRecord>> CreateChunkWriter(
		IChunkReaderForExecutor<TStreamId, TRecord> sourceChunk,
		CancellationToken token);

	IChunkReaderForExecutor<TStreamId, TRecord> GetChunkReaderFor(long position);
}

public interface IChunkWriterForExecutor<TStreamId, TRecord> {
	string FileName { get; }

	ValueTask WriteRecord(RecordForExecutor<TStreamId, TRecord> record, CancellationToken token);

	ValueTask<(string NewFileName, long NewFileSize)> Complete(CancellationToken token);

	void Abort(bool deleteImmediately);
}

public interface IChunkReaderForExecutor<TStreamId, TRecord> {
	string Name { get; }
	int FileSize { get; }
	int ChunkStartNumber { get; }
	int ChunkEndNumber { get; }
	bool IsReadOnly { get; }
	bool IsRemote { get; }
	long ChunkStartPosition { get; }
	long ChunkEndPosition { get; }
	IAsyncEnumerable<bool> ReadInto(
		RecordForExecutor<TStreamId, TRecord>.NonPrepare nonPrepare,
		RecordForExecutor<TStreamId, TRecord>.Prepare prepare,
		CancellationToken token);
}
