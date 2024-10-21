// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading;
using System.Threading.Tasks;
using DotNext;
using EventStore.Core.Data;
using EventStore.Core.LogAbstraction;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;
using StreamId = System.UInt32;

namespace EventStore.Core.LogV3;

public class StreamIdToNameFromStandardIndex : INameLookup<StreamId> {
	private readonly IIndexReader<StreamId> _indexReader;

	public StreamIdToNameFromStandardIndex(IIndexReader<StreamId> indexReader) {
		_indexReader = indexReader;
	}

	public async ValueTask<string> LookupName(StreamId streamId, CancellationToken token) {
		if (streamId % 2 is 1)
			throw new ArgumentOutOfRangeException(nameof(streamId), "streamId must be even");

		// we divided by two when calculating the position in the stream, since we dont
		// explicitly create metastreams.
		var record = await _indexReader.ReadPrepare(
			streamId: LogV3SystemStreams.StreamsCreatedStreamNumber,
			eventNumber: StreamIdConverter.ToEventNumber(streamId),
			token);

		return record switch {
			null => null,
			LogV3StreamRecord streamRecord => streamRecord.StreamName,
			_ => throw new Exception($"Unexpected log record type: {record}.")
		};
	}

	public async ValueTask<Optional<StreamId>> TryGetLastValue(CancellationToken token) {
		var lastEventNumber =
			await _indexReader.GetStreamLastEventNumber(LogV3SystemStreams.StreamsCreatedStreamNumber, token);

		return lastEventNumber is > ExpectedVersion.NoStream and not EventNumber.DeletedStream
			? StreamIdConverter.ToStreamId(lastEventNumber)
			: Optional.None<StreamId>();
	}
}
