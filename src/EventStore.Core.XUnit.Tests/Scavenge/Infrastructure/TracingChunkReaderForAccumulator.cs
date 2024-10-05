// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using EventStore.Core.TransactionLog.Scavenging;

namespace EventStore.Core.XUnit.Tests.Scavenge;

public class TracingChunkReaderForAccumulator<TStreamId> : IChunkReaderForAccumulator<TStreamId> {
	private readonly IChunkReaderForAccumulator<TStreamId> _wrapped;
	private readonly Action<string> _trace;

	public TracingChunkReaderForAccumulator(
		IChunkReaderForAccumulator<TStreamId> wrapped,
		Action<string> trace) {

		_wrapped = wrapped;
		_trace = trace;
	}

	public IEnumerable<AccumulatorRecordType> ReadChunkInto(
		int logicalChunkNumber,
		RecordForAccumulator<TStreamId>.OriginalStreamRecord originalStreamRecord,
		RecordForAccumulator<TStreamId>.MetadataStreamRecord metadataStreamRecord,
		RecordForAccumulator<TStreamId>.TombStoneRecord tombStoneRecord) {

		var ret = _wrapped.ReadChunkInto(
			logicalChunkNumber,
			originalStreamRecord,
			metadataStreamRecord,
			tombStoneRecord);

		_trace($"Reading Chunk {logicalChunkNumber}");
		return ret;
	}
}
