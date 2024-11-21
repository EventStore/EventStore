#nullable enable

using System.Collections.Concurrent;
using System.Collections.Generic;
using EventStore.Core.Metrics;

namespace EventStore.Core.TransactionLog.Chunks;

public class TransactionFileTrackerFactory : ITransactionFileTrackerFactory {
	private readonly ConcurrentDictionary<string, ITransactionFileTracker> _trackersByUser = new();
	private readonly CounterMetric _eventMetric;
	private readonly CounterMetric _byteMetric;

	public TransactionFileTrackerFactory(CounterMetric eventMetric, CounterMetric byteMetric) {
		_eventMetric = eventMetric;
		_byteMetric = byteMetric;
	}

	public ITransactionFileTracker GetOrAdd(string user) {
		return _trackersByUser.GetOrAdd(user, Create);
	}

	private ITransactionFileTracker Create(string user) {
		var readTag = new KeyValuePair<string, object>("activity", "read");
		var cachedTag = new KeyValuePair<string, object>("cached", "true");
		var uncachedTag = new KeyValuePair<string, object>("cached", "false");
		var userTag = new KeyValuePair<string, object>("user", user);

		var tracker = new TFChunkTracker(
			readCachedBytes: new CounterSubMetric(_byteMetric, [readTag, cachedTag, userTag]),
			readCachedEvents: new CounterSubMetric(_eventMetric, [readTag, cachedTag, userTag]),
			readUncachedBytes: new CounterSubMetric(_byteMetric, [readTag, uncachedTag, userTag]),
			readUncachedEvents: new CounterSubMetric(_eventMetric, [readTag, uncachedTag, userTag]));

		return tracker;
	}

	public void Clear() {
		_trackersByUser.Clear();
	}
}
