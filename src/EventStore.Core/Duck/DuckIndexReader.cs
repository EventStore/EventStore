// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Data;
using EventStore.Core.Duck.Default;
using EventStore.Core.Services.Storage;
using EventStore.Core.Services.Storage.ReaderIndex;
using static EventStore.Core.Messages.ClientMessage;
using ReadStreamResult = EventStore.Core.Data.ReadStreamResult;

namespace EventStore.Core.Duck;

public static class DuckIndexReader {
	public static async Task<ReadStreamEventsBackwardCompleted> ReadBackwards<TStreamId>(
		ReadStreamEventsBackward msg, IIndexReader<TStreamId> reader, long lastIndexedPosition, CancellationToken token
	) {
		var lastEventNumber = CategoryIndex.GetCategoryLastEventNumber(msg.EventStreamId);

		if (msg.ValidationStreamVersion.HasValue && lastEventNumber == msg.ValidationStreamVersion)
			return StorageReaderWorker<TStreamId>.NoData(msg, ReadStreamResult.NotModified, lastIndexedPosition, msg.ValidationStreamVersion.Value);
		if (lastEventNumber == 0)
			return StorageReaderWorker<TStreamId>.NoData(msg, ReadStreamResult.NoStream, lastIndexedPosition, msg.ValidationStreamVersion ?? 0);

		long endEventNumber = msg.FromEventNumber < 0 ? lastEventNumber : msg.FromEventNumber;
		long startEventNumber = Math.Max(0L, endEventNumber - msg.MaxCount + 1);
		var resolved = await CategoryIndex.GetCategoryEvents(reader, msg.EventStreamId, startEventNumber, endEventNumber, token);

		if (resolved.Count == 0)
			return StorageReaderWorker<TStreamId>.NoData(msg, ReadStreamResult.Success, lastIndexedPosition, msg.ValidationStreamVersion ?? 0);

		var records = resolved.OrderByDescending(x => x.OriginalEvent.EventNumber).ToArray();
		var isEndOfStream = startEventNumber == 0 || (startEventNumber <= lastEventNumber && (records.Length is 0 || records[^1].OriginalEventNumber != startEventNumber));
		long nextEventNumber = isEndOfStream ? -1 : Math.Min(startEventNumber - 1, lastEventNumber);

		return new(msg.CorrelationId, msg.EventStreamId, endEventNumber, msg.MaxCount,
			ReadStreamResult.Success, records, StreamMetadata.Empty, false, string.Empty,
			nextEventNumber, lastEventNumber, isEndOfStream, lastIndexedPosition);
	}

	public static async Task<ReadStreamEventsForwardCompleted> ReadForwards<TStreamId>(
		ReadStreamEventsForward msg, IIndexReader<TStreamId> reader, long lastIndexedPosition, CancellationToken token
	) {
		var lastEventNumber = CategoryIndex.GetCategoryLastEventNumber(msg.EventStreamId);

		if (msg.ValidationStreamVersion.HasValue && lastEventNumber == msg.ValidationStreamVersion)
			return StorageReaderWorker<TStreamId>.NoData(msg, ReadStreamResult.NotModified, lastIndexedPosition, msg.ValidationStreamVersion.Value);

		var fromEventNumber = msg.FromEventNumber < 0 ? 0 : msg.FromEventNumber;
		var maxCount = msg.MaxCount;
		var endEventNumber = fromEventNumber > long.MaxValue - maxCount + 1 ? long.MaxValue : fromEventNumber + maxCount - 1;
		var resolved = await CategoryIndex.GetCategoryEvents(reader, msg.EventStreamId, fromEventNumber, endEventNumber, token);

		if (resolved.Count == 0)
			return StorageReaderWorker<TStreamId>.NoData(msg, ReadStreamResult.Success, lastIndexedPosition, msg.ValidationStreamVersion ?? 0);

		long nextEventNumber = Math.Min(endEventNumber + 1, lastEventNumber + 1);
		if (resolved.Count > 0)
			nextEventNumber = resolved[^1].OriginalEventNumber + 1;
		var isEndOfStream = endEventNumber >= lastEventNumber;

		return new(msg.CorrelationId, msg.EventStreamId, msg.FromEventNumber, msg.MaxCount,
			ReadStreamResult.Success, resolved, StreamMetadata.Empty, false, string.Empty,
			nextEventNumber, lastEventNumber, isEndOfStream, lastIndexedPosition);
	}
}
