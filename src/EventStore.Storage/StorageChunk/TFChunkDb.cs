using System;
using EventStore.Core.TransactionLogV2;
using EventStore.Core.TransactionLogV2.Chunks;
using V2TFChunkDb = EventStore.Core.TransactionLogV2.Chunks.TFChunkDb;

namespace EventStore.Core.Services.Storage.StorageChunk {
	public interface TFChunkDb : IDisposable {
		TFChunkDbConfig Config { get; }
		// TODO: Don't expose this
		V2TFChunkDb Db { get; }
		TFChunkManager Manager { get; }
		TFChunkDbTruncator Truncator { get; }

		void Close();
		void Open(bool verifyHash = true, bool readOnly = false, int threads = 1);
		TFChunkReader GetReader();
		TFChunkWriter GetWriter();
		ITransactionFileChaser GetChunkChaser();
	}
}
