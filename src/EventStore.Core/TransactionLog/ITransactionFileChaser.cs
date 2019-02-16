using System;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog {
	public interface ITransactionFileChaser : IDisposable {
		ICheckpoint Checkpoint { get; }

		void Open();

		SeqReadResult TryReadNext();
		bool TryReadNext(out LogRecord record);

		void Close();
		void Flush();
	}
}
