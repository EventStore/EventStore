using System;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog {
	public interface ITransactionFileWriter : IDisposable {
		void Open();
		bool Write(LogRecord record, out long newPos);
		void Flush();
		void Close();

		ICheckpoint Checkpoint { get; }
	}
}
