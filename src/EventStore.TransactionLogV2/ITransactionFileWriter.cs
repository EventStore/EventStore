using System;
using EventStore.Core.TransactionLogV2.Checkpoint;
using EventStore.Core.TransactionLogV2.LogRecords;

namespace EventStore.Core.TransactionLogV2 {
	public interface ITransactionFileWriter : IDisposable {
		void Open();
		bool Write(LogRecord record, out long newPos);
		void Flush();
		void Close();

		ICheckpoint Checkpoint { get; }
	}
}
