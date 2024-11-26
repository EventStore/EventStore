#nullable enable

using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog;

public interface ITransactionFileTracker {
	void OnRead(ILogRecord record, Source source);
	void OnRead(int bytesRead, Source source);

	enum Source {
		Unknown,
		Archive,
		ChunkCache,
		File,
		EnumLength,
	};

	static readonly ITransactionFileTracker NoOp = new NoOp();
}

file class NoOp : ITransactionFileTracker {
	public void OnRead(ILogRecord record, ITransactionFileTracker.Source source) { }
	public void OnRead(int bytesRead, ITransactionFileTracker.Source source) { }
}
