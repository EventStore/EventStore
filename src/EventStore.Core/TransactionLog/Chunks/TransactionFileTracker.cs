#nullable enable
using EventStore.Core.Telemetry;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog.Chunks;

public class TFChunkTracker : ITransactionFileTracker {
	private readonly CounterSubMetric<long> _readBytes;
	private readonly CounterSubMetric<long> _readEvents;

	public TFChunkTracker(
		CounterSubMetric<long> readBytes,
		CounterSubMetric<long> readEvents) {

		_readBytes = readBytes;
		_readEvents = readEvents;
	}

	public void OnRead(ILogRecord record) {
		if (record is not PrepareLogRecord prepare)
			return;

		_readBytes.Add(prepare.Data.Length + prepare.Metadata.Length);
		_readEvents.Add(1);
	}

	public class NoOp : ITransactionFileTracker {
		public void OnRead(ILogRecord record) {
		}
	}
}
