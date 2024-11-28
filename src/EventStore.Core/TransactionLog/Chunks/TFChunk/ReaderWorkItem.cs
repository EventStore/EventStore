#nullable enable

using System.IO;

namespace EventStore.Core.TransactionLog.Chunks.TFChunk {
	// ReaderWorkItems are always checked out of a pool and used by one thread at a time
	internal class ReaderWorkItem {
		public readonly Stream Stream;
		public readonly BinaryReader Reader;
		public readonly bool IsMemory;

		public ReaderWorkItem(Stream stream, BinaryReader reader, bool isMemory) {
			Stream = stream;
			Reader = reader;
			IsMemory = isMemory;
		}

		public ITransactionFileTracker Tracker { get; private set; } = ITransactionFileTracker.NoOp;

		public void OnCheckedOut(ITransactionFileTracker tracker) {
			Tracker = tracker;
		}

		public void OnReturning() {
			Tracker = ITransactionFileTracker.NoOp;
		}
	}
}
