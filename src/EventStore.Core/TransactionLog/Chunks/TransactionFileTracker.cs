#nullable enable
using EventStore.Core.Metrics;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog.Chunks;

public class TFChunkTracker : ITransactionFileTracker {
	private readonly CounterSubMetric _readBytes;
	private readonly CounterSubMetric _readEvents;

	public TFChunkTracker(
		CounterSubMetric readBytes,
		CounterSubMetric readEvents) {

		_readBytes = readBytes;
		_readEvents = readEvents;
	}

	public void OnRead(ILogRecord record) {
		if (record is not PrepareLogRecord prepare)
			return;

		_readBytes.Add(prepare.Data.Length + prepare.Metadata.Length);
		_readEvents.Add(1);
	}
}
