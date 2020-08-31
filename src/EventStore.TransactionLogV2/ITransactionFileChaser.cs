using System;
using EventStore.Core.TransactionLogV2.Checkpoint;
using EventStore.Core.TransactionLogV2.LogRecords;

namespace EventStore.Core.TransactionLogV2 {
	public interface ITransactionFileChaser : IDisposable {
		ICheckpoint Checkpoint { get; }

		void Open();

		SeqReadResult TryReadNext();
		bool TryReadNext(out LogRecord record);

		void Close();
		void Flush();
	}
}
