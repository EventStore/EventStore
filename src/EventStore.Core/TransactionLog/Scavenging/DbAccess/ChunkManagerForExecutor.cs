using System;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog.Scavenging {
	public class ChunkManagerForExecutor : IChunkManagerForChunkExecutor<string, LogRecord> {
		private readonly TFChunkManager _manager;
		private readonly TFChunkDbConfig _dbConfig;

		public ChunkManagerForExecutor(TFChunkManager manager, TFChunkDbConfig dbConfig) {
			_manager = manager;
			_dbConfig = dbConfig;
		}

		public IChunkWriterForExecutor<string, LogRecord> CreateChunkWriter(
			IChunkReaderForExecutor<string, LogRecord> sourceChunk) {

			return new ChunkWriterForExecutor(this, _dbConfig, sourceChunk);
		}

		public IChunkReaderForExecutor<string, LogRecord> GetChunkReaderFor(long position) {
			var tfChunk = _manager.GetChunkFor(position);
			return new ChunkReaderForExecutor(tfChunk);
		}

		public void SwitchChunk(
			TFChunk chunk,
			out string newFileName) {

			var tfChunk = _manager.SwitchChunk(
				chunk: chunk,
				verifyHash: false,
				removeChunksWithGreaterNumbers: false);

			if (tfChunk == null) {
				throw new Exception("Unexpected error: new chunk is null after switch");
			}

			newFileName = tfChunk.FileName;
		}
	}
}
