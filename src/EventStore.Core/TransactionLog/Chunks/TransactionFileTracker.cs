#nullable enable

using System.Collections.Generic;
using EventStore.Core.Metrics;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog.Chunks;

public class TFChunkTracker : ITransactionFileTracker {
	private readonly (CounterSubMetric Events, CounterSubMetric Bytes)[] _subMetrics;

	public TFChunkTracker(CounterMetric eventMetric, CounterMetric byteMetric, string user) {
		_subMetrics = new (CounterSubMetric, CounterSubMetric)[(int)(ITransactionFileTracker.Source.EnumLength)];
		for (var i = 0; i < _subMetrics.Length; i++) {
			var sourceName = NameOf((ITransactionFileTracker.Source)i);
			_subMetrics[i] = (
				Events: CreateSubMetric(eventMetric, sourceName, user),
				Bytes: CreateSubMetric(byteMetric, sourceName, user));
		}
	}

	static string NameOf(ITransactionFileTracker.Source source) => source switch {
		ITransactionFileTracker.Source.Archive => "archive",
		ITransactionFileTracker.Source.ChunkCache => "chunk-cache",
		ITransactionFileTracker.Source.File => "file",
		_ => "unknown",
	};

	static CounterSubMetric CreateSubMetric(CounterMetric metric, string source, string user) {
		var readTag = new KeyValuePair<string, object>("activity", "read");
		var sourceTag = new KeyValuePair<string, object>("source", source);
		var userTag = new KeyValuePair<string, object>("user", user);
		return new CounterSubMetric(metric, [readTag, sourceTag, userTag]);
	}

	public void OnRead(ILogRecord record, ITransactionFileTracker.Source source) {
		if (record is not PrepareLogRecord prepare)
			return;

		var subMetrics = _subMetrics[(int)source];
		subMetrics.Bytes.Add(prepare.Data.Length + prepare.Metadata.Length); // approximate
		subMetrics.Events.Add(1);
	}

	public void OnRead(int bytesRead, ITransactionFileTracker.Source source) {
		var subMetrics = _subMetrics[(int)source];
		subMetrics.Bytes.Add(bytesRead);
	}
}
