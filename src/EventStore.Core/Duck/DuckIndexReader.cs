// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Data;
using EventStore.Core.Duck.Default;
using EventStore.Core.Services.Storage;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.LogCommon;
using static EventStore.Core.Messages.ClientMessage;
using ReadStreamResult = EventStore.Core.Data.ReadStreamResult;

namespace EventStore.Core.Duck;

public record struct IndexedPrepare(long Version, long StreamId, long EventType, int EventNumber, long LogPosition);

abstract class DuckIndexReader(StreamIndex streamIndex, EventTypeIndex eventTypeIndex) {
	protected abstract long GetId(string streamName);

	protected abstract long GetLastNumber(long id);

	protected abstract IEnumerable<IndexedPrepare> GetIndexRecords(long id, long fromEventNumber, long toEventNumber);

	public async ValueTask<IReadOnlyList<ResolvedEvent>> GetEvents<TStreamId>(IIndexReader<TStreamId> index, long id, long fromEventNumber, long toEventNumber,
		CancellationToken cancellationToken) {
		var indexPrepares = GetIndexRecords(id, fromEventNumber, toEventNumber);
		using var reader = index.BorrowReader();
		var readPrepares = indexPrepares.Select(async x => (Record: x, Prepare: await ReadPrepare(x.LogPosition, cancellationToken)));
		var prepared = await Task.WhenAll(readPrepares);
		var recordsQuery = prepared.Where(x => x.Prepare != null).OrderBy(x => x.Record.Version).ToList();
		var streams = streamIndex.GetStreams(recordsQuery.Select(x => x.Record.StreamId).Distinct());
		var records = recordsQuery
			.Select(x => (Record: x, StreamName: streams[x.Record.StreamId]))
			.Select(x => ResolvedEvent.ForResolvedLink(
				new(x.Record.Record.EventNumber, x.Record.Prepare, x.StreamName, eventTypeIndex.EventTypeIds[x.Record.Record.EventType]),
				new(
					x.Record.Record.Version,
					x.Record.Prepare.LogPosition,
					x.Record.Prepare.CorrelationId,
					x.Record.Prepare.EventId,
					x.Record.Prepare.TransactionPosition,
					x.Record.Prepare.TransactionOffset,
					x.StreamName,
					x.Record.Record.Version,
					x.Record.Prepare.TimeStamp,
					x.Record.Prepare.Flags,
					"$>",
					Encoding.UTF8.GetBytes($"{x.Record.Record.EventNumber}@{x.StreamName}"),
					[]
				))
			);
		var result = records.ToList();
		return result;

		async ValueTask<IPrepareLogRecord<TStreamId>> ReadPrepare(long logPosition, CancellationToken ct) {
			var r = await reader.TryReadAt(logPosition, couldBeScavenged: true, ct);
			if (!r.Success)
				return null;

			if (r.LogRecord.RecordType is not LogRecordType.Prepare
			    and not LogRecordType.Stream
			    and not LogRecordType.EventType)
				throw new($"Incorrect type of log record {r.LogRecord.RecordType}, expected Prepare record.");
			return (IPrepareLogRecord<TStreamId>)r.LogRecord;
		}
	}


	public async Task<ReadStreamEventsBackwardCompleted> ReadBackwards<TStreamId>(
		ReadStreamEventsBackward msg, IIndexReader<TStreamId> reader, long lastIndexedPosition, CancellationToken token
	) {
		var id = GetId(msg.EventStreamId);
		var lastEventNumber = GetLastNumber(id);

		if (msg.ValidationStreamVersion.HasValue && lastEventNumber == msg.ValidationStreamVersion)
			return StorageReaderWorker<TStreamId>.NoData(msg, ReadStreamResult.NotModified, lastIndexedPosition, msg.ValidationStreamVersion.Value);
		if (lastEventNumber == 0)
			return StorageReaderWorker<TStreamId>.NoData(msg, ReadStreamResult.NoStream, lastIndexedPosition, msg.ValidationStreamVersion ?? 0);

		long endEventNumber = msg.FromEventNumber < 0 ? lastEventNumber : msg.FromEventNumber;
		long startEventNumber = Math.Max(0L, endEventNumber - msg.MaxCount + 1);
		var resolved = await GetEvents(reader, id, startEventNumber, endEventNumber, token);

		if (resolved.Count == 0)
			return StorageReaderWorker<TStreamId>.NoData(msg, ReadStreamResult.Success, lastIndexedPosition, msg.ValidationStreamVersion ?? 0);

		var records = resolved.OrderByDescending(x => x.OriginalEvent.EventNumber).ToArray();
		var isEndOfStream = startEventNumber == 0 || (startEventNumber <= lastEventNumber && (records.Length is 0 || records[^1].OriginalEventNumber != startEventNumber));
		long nextEventNumber = isEndOfStream ? -1 : Math.Min(startEventNumber - 1, lastEventNumber);

		return new(msg.CorrelationId, msg.EventStreamId, endEventNumber, msg.MaxCount,
			ReadStreamResult.Success, records, StreamMetadata.Empty, false, string.Empty,
			nextEventNumber, lastEventNumber, isEndOfStream, lastIndexedPosition);
	}

	public async Task<ReadStreamEventsForwardCompleted> ReadForwards<TStreamId>(
		ReadStreamEventsForward msg, IIndexReader<TStreamId> reader, long lastIndexedPosition, CancellationToken token
	) {
		var id = GetId(msg.EventStreamId);
		var lastEventNumber = GetLastNumber(id);

		if (msg.ValidationStreamVersion.HasValue && lastEventNumber == msg.ValidationStreamVersion)
			return StorageReaderWorker<TStreamId>.NoData(msg, ReadStreamResult.NotModified, lastIndexedPosition, msg.ValidationStreamVersion.Value);

		var fromEventNumber = msg.FromEventNumber < 0 ? 0 : msg.FromEventNumber;
		var maxCount = msg.MaxCount;
		var endEventNumber = fromEventNumber > long.MaxValue - maxCount + 1 ? long.MaxValue : fromEventNumber + maxCount - 1;
		var resolved = await GetEvents(reader, id, fromEventNumber, endEventNumber, token);

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
