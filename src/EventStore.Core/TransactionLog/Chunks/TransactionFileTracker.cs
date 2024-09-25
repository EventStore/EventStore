#nullable enable
using EventStore.Core.Metrics;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog.Chunks;

public class TFChunkTracker : ITransactionFileTracker {
	private readonly LogicalChunkReadDistributionMetric _readDistribution;
	private readonly CounterSubMetric _readBytes;
	private readonly CounterSubMetric _readEvents;

	public TFChunkTracker(
		LogicalChunkReadDistributionMetric readDistribution,
		CounterSubMetric readBytes,
		CounterSubMetric readEvents) {

		_readBytes = readBytes;
		_readEvents = readEvents;
		_readDistribution = readDistribution;
	}

	public void OnRead(ILogRecord record) {
		_readDistribution.Record(record);

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
