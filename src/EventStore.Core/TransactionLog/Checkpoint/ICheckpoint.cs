using System;

namespace EventStore.Core.TransactionLog.Checkpoint {
	public interface ICheckpoint : IReadOnlyCheckpoint {
		void Write(long checkpoint);
		void Flush();
		void Close(bool flush);
		IReadOnlyCheckpoint AsReadOnly() => this;
	}

	public interface IReadOnlyCheckpoint {
		string Name { get; }
		long Read();
		long ReadNonFlushed();

		event Action<long> Flushed;
	}
}
