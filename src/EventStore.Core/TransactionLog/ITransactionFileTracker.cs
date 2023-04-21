using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog;

public interface ITransactionFileTracker {
	void OnRead(ILogRecord record);
}
