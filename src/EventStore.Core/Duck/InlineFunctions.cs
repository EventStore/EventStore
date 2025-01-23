using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DuckDB.NET.Data;
using DuckDB.NET.Data.Reader;
using DuckDB.NET.Data.Writer;
using EventStore.Common.Utils;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog;
using Serilog;

namespace EventStore.Core.Duck;

public class InlineFunctions<TStreamId>(DuckDb db, IIndexReader<TStreamId> index) {
	static readonly IReadOnlyList<ColumnInfo> ColumnInfos = [
		new("stream_id", typeof(TStreamId)),
		new("event_number", typeof(long)),
		new("event_type", typeof(string)),
		new("created", typeof(DateTime)),
		new("data", typeof(string)),
		new("metadata", typeof(string)),
		new("log_position", typeof(ulong))
	];

	static readonly ILogger log = Log.ForContext<InlineFunctions<TStreamId>>();

	[Experimental("DuckDBNET001")]
	public void Run() {
		// db.Connection.RegisterTableFunction<ulong>("kdb_all", ReadAllResultCallback, ReadAllMapperCallback);
		// db.Connection.RegisterTableFunction<string>("kdb_stream", ReadStreamResultCallback, ReadAllMapperCallback);
		db.Connection.RegisterScalarFunction<ulong, string>("kdb_get", GetEvent);
	}

	// TableFunction ReadStreamResultCallback(IReadOnlyList<IDuckDBValueReader> parameters) {
	//     var stream = parameters[0].GetValue<string>();
	//
	//     return new(ColumnInfos, ReadStream(stream, StreamPosition.Start, long.MaxValue));
	// }

	// TableFunction ReadAllResultCallback(IReadOnlyList<IDuckDBValueReader> parameters) {
	//     var startPosition = parameters[0].GetValue<ulong>();
	//     var start         = startPosition == 0 ? Position.Start : new(startPosition, startPosition);
	//
	//     return new(ColumnInfos, ReadAll(start));
	// }

	// [Experimental("DuckDBNET001")]
	// static void ReadAllMapperCallback(object item, IDuckDBDataWriter[] writers, ulong rowIndex) {
	//     var evt = ((ResolvedEvent)item!).Event;
	//
	//     writers[0].WriteValue(evt.EventStreamId, rowIndex);
	//     writers[1].WriteValue(evt.EventNumber, rowIndex);
	//     writers[2].WriteValue(evt.EventType, rowIndex);
	//     writers[3].WriteValue(evt.Created, rowIndex);
	//     writers[4].WriteValue(Encoding.UTF8.GetString(evt.Data.Span), rowIndex);
	//     writers[5].WriteValue(evt.Metadata.Length == 0 ? "{}" : Encoding.UTF8.GetString(evt.Metadata.Span), rowIndex);
	//     writers[6].WriteValue(evt.Position.CommitPosition, rowIndex);
	// }

	// ReSharper disable once CognitiveComplexity
	// IEnumerable ReadStream(string stream, StreamPosition start, long count) {
	//     log.LogDebug("Reading stream {Stream} from {Position}", stream, start);
	//     var result = client.ReadStreamAsync(Direction.Forwards, stream, start, count, resolveLinkTos: true, deadline: TimeSpan.FromHours(1));
	//
	//     foreach (var evt in result.Messages.ToEnumerable()) {
	//         if (evt is not StreamMessage.Event e || e.ResolvedEvent.Event == null) {
	//             continue;
	//         }
	//
	//         yield return e.ResolvedEvent;
	//     }
	// }

	// IEnumerable ReadAll(Position start) {
	//     var result = client.ReadAllAsync(Direction.Forwards, start, EventTypeFilter.ExcludeSystemEvents(), deadline: TimeSpan.FromHours(1));
	//
	//     foreach (var evt in result.Messages.ToEnumerable()) {
	//         if (evt is not StreamMessage.Event e) {
	//             log.LogInformation("Type: {Type}", evt.GetType().Name);
	//
	//             continue;
	//         }
	//
	//         yield return e.ResolvedEvent;
	//     }
	// }

	[Experimental("DuckDBNET001")]
	void GetEvent(IReadOnlyList<IDuckDBDataReader> readers, IDuckDBDataWriter writer, ulong rowCount) {
		log.Debug("Retrieving {Count} events", rowCount);
		var watch = new Stopwatch();
		watch.Start();
		var tasks = new List<Task<(ulong, string)>>();

		using var reader = index.BorrowReader();
		for (ulong i = 0; i < rowCount; i++) {
			var value = readers[0].GetValue<ulong>(i);
			tasks.Add(ReadEvent(reader, i, (long)value));
		}

		var chunks = tasks.Chunk(512);

		foreach (var chunk in chunks) {
			var results = Task.Run(() => Task.WhenAll(chunk)).GetAwaiter().GetResult();

			foreach (var result in results) {
				writer.WriteValue(result.Item2, result.Item1);
			}
		}

		watch.Stop();
		log.Debug("Retrieved in {Duration}", watch);
	}

	static async Task<(ulong Index, string Event)> ReadEvent(TFReaderLease reader, ulong i, long position) {
		var result = await reader.ReadPrepare<TStreamId>(position, CancellationToken.None).ConfigureAwait(false);

		var data = Encoding.UTF8.GetString(result.Data.Span);
		var meta = result.Metadata.Length == 0 ? "{}" : Helper.UTF8NoBom.GetString(result.Metadata.Span);

		return (i, $"{{ \"data\": {data}, \"metadata\": {meta} }}");
	}
}
