#nullable enable

using EventStore.Core.Metrics;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog.Chunks;

public class TFChunkTracker : ITransactionFileTracker {
	private readonly CounterSubMetric _readCachedBytes;
	private readonly CounterSubMetric _readCachedEvents;
	private readonly CounterSubMetric _readUncachedBytes;
	private readonly CounterSubMetric _readUncachedEvents;

	public TFChunkTracker(
		CounterSubMetric readCachedBytes,
		CounterSubMetric readCachedEvents,
		CounterSubMetric readUncachedBytes,
		CounterSubMetric readUncachedEvents) {

		_readCachedBytes = readCachedBytes;
		_readCachedEvents = readCachedEvents;
		_readUncachedBytes = readUncachedBytes;
		_readUncachedEvents = readUncachedEvents;
	}

	public void OnRead(ILogRecord record, bool cached) {
		if (record is not PrepareLogRecord prepare)
			return;

		if (cached) {
			_readCachedBytes.Add(prepare.Data.Length + prepare.Metadata.Length);
			_readCachedEvents.Add(1);
		} else {
			_readUncachedBytes.Add(prepare.Data.Length + prepare.Metadata.Length);
			_readUncachedEvents.Add(1);
		}
	}
}
