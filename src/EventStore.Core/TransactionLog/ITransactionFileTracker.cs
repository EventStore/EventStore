using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog;

public interface ITransactionFileTracker {
	void OnRead(ILogRecord record);

	static readonly ITransactionFileTracker NoOp = new NoOp();
}

file class NoOp : ITransactionFileTracker {
	public void OnRead(ILogRecord record) {
	}
}
