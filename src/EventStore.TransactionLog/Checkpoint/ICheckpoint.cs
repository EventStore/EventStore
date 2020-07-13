using System;

namespace EventStore.Core.TransactionLog.Checkpoint {
	public interface ICheckpoint : IDisposable {
		string Name { get; }
		void Write(long checkpoint);
		void Flush();
		void Close();
		long Read();
		long ReadNonFlushed();

		event Action<long> Flushed;
	}
}
