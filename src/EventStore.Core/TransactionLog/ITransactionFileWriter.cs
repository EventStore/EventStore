using System;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog {
	public interface ITransactionFileWriter : IDisposable {
		void Open();
		bool Write(ILogRecord record, out long newPos);
		void OpenTransaction();
		bool WriteToTransaction(ILogRecord record, out long newPos);
		void CommitTransaction();
		bool HasOpenTransaction();
		void Flush();
		void Close();

		long Position { get; }
		long FlushedPosition { get; }
	}
}
