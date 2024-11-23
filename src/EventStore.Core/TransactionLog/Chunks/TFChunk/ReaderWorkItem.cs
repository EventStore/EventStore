#nullable enable

using System.IO;

namespace EventStore.Core.TransactionLog.Chunks.TFChunk {
	// ReaderWorkItems are checked out of a pool and used by one thread at a time
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

		//qq is this always called?
		public void OnCheckedOut(ITransactionFileTracker tracker) {
			Tracker = tracker;
		}

		//qq rename, this needs to be called before being returned
		public void OnReturned() {
			Tracker = ITransactionFileTracker.NoOp;
		}
	}
}
