#nullable enable

using System.Collections.Concurrent;
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
		var tracker = new TFChunkTracker(_eventMetric, _byteMetric, user);
		return tracker;
	}

	public void Clear() {
		_trackersByUser.Clear();
	}
}
