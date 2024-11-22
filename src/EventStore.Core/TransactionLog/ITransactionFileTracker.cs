#nullable enable

using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog;

public interface ITransactionFileTracker {
	void OnRead(ILogRecord record, Source source);

	enum Source {
		Unknown,
		Archive,
		ChunkCache,
		Disk,
		EnumLength,
	};

	static readonly ITransactionFileTracker NoOp = new NoOp();
}

file class NoOp : ITransactionFileTracker {
	public void OnRead(ILogRecord record, ITransactionFileTracker.Source source) {
	}
}
