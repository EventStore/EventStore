#nullable enable

using System.Collections.Generic;
using EventStore.Core.Metrics;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog.Chunks;

public class TFChunkTracker : ITransactionFileTracker {
	private readonly (CounterSubMetric, CounterSubMetric)[] _subMetrics;

	public TFChunkTracker(CounterMetric eventMetric, CounterMetric byteMetric, string user) {
		_subMetrics = new (CounterSubMetric, CounterSubMetric)[(int)(ITransactionFileTracker.Source.EnumLength)];
		for (var i = 0; i < _subMetrics.Length; i++) {
			var source = $"{(ITransactionFileTracker.Source)i}";
			_subMetrics[i] = (
				CreateSubMetric(eventMetric, source, user),
				CreateSubMetric(byteMetric, source, user));
		}
	}

	static CounterSubMetric CreateSubMetric(CounterMetric metric, string source, string user) {
		var readTag = new KeyValuePair<string, object>("activity", "read");
		var sourceTag = new KeyValuePair<string, object>("source", source);
		var userTag = new KeyValuePair<string, object>("user", user);
		return new CounterSubMetric(metric, [readTag, sourceTag, userTag]);
	}

	public void OnRead(ILogRecord record, ITransactionFileTracker.Source source) {
		if (record is not PrepareLogRecord prepare)
			return;

		var (bytes, events) = _subMetrics[(int)source];
		bytes.Add(prepare.Data.Length + prepare.Metadata.Length);
		events.Add(1);
	}
}
