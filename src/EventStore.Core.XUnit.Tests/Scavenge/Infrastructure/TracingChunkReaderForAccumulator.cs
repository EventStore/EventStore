// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Threading;
using EventStore.Core.TransactionLog.Scavenging;
using EventStore.Core.TransactionLog.Scavenging.Interfaces;

namespace EventStore.Core.XUnit.Tests.Scavenge.Infrastructure;

public class TracingChunkReaderForAccumulator<TStreamId> : IChunkReaderForAccumulator<TStreamId> {
	private readonly IChunkReaderForAccumulator<TStreamId> _wrapped;
	private readonly Action<string> _trace;

	public TracingChunkReaderForAccumulator(
		IChunkReaderForAccumulator<TStreamId> wrapped,
		Action<string> trace) {

		_wrapped = wrapped;
		_trace = trace;
	}

	public IAsyncEnumerable<AccumulatorRecordType> ReadChunkInto(
		int logicalChunkNumber,
		RecordForAccumulator<TStreamId>.OriginalStreamRecord originalStreamRecord,
		RecordForAccumulator<TStreamId>.MetadataStreamRecord metadataStreamRecord,
		RecordForAccumulator<TStreamId>.TombStoneRecord tombStoneRecord,
		CancellationToken token) {

		var ret = _wrapped.ReadChunkInto(
			logicalChunkNumber,
			originalStreamRecord,
			metadataStreamRecord,
			tombStoneRecord,
			token);

		_trace($"Reading Chunk {logicalChunkNumber}");
		return ret;
	}
}
