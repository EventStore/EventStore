// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.LogCommon;

namespace EventStore.Core.Duck;

public static class ReaderExtensions {
	public static async ValueTask<IReadOnlyList<ResolvedEvent>> ReadRecords<TStreamId>(this IIndexReader<TStreamId> index, IEnumerable<IndexedPrepare> indexPrepares,
		CancellationToken cancellationToken) {
		using var reader = index.BorrowReader();
		// ReSharper disable once AccessToDisposedClosure
		var readPrepares = indexPrepares.Select(async x => (Record: x, Prepare: await reader.ReadPrepare<TStreamId>(x.LogPosition, cancellationToken)));
		var prepared = await Task.WhenAll(readPrepares);
		var recordsQuery = prepared.Where(x => x.Prepare != null).OrderBy(x => x.Record.Version).ToList();
		var records = recordsQuery
			.Select(x => (Record: x, StreamName: x.Prepare.EventStreamId.ToString()))
			.Select(x => ResolvedEvent.ForResolvedLink(
				new(x.Record.Record.EventNumber, x.Record.Prepare, x.StreamName, x.Record.Prepare.EventType.ToString()),
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
	}

		internal static async ValueTask<IPrepareLogRecord<TStreamId>> ReadPrepare<TStreamId>(this TFReaderLease localReader, long logPosition, CancellationToken ct) {
			var r = await localReader.TryReadAt(logPosition, couldBeScavenged: true, ct);
			if (!r.Success)
				return null;

			if (r.LogRecord.RecordType is not LogRecordType.Prepare
			    and not LogRecordType.Stream
			    and not LogRecordType.EventType)
				throw new($"Incorrect type of log record {r.LogRecord.RecordType}, expected Prepare record.");
			return (IPrepareLogRecord<TStreamId>)r.LogRecord;
		}
}
