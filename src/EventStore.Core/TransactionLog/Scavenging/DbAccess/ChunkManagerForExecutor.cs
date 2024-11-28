using System;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using EventStore.Core.TransactionLog.LogRecords;
using Serilog;

namespace EventStore.Core.TransactionLog.Scavenging {
	public class ChunkManagerForExecutor<TStreamId> : IChunkManagerForChunkExecutor<TStreamId, ILogRecord> {
		private readonly ILogger _logger;
		private readonly TFChunkManager _manager;
		private readonly TFChunkDbConfig _dbConfig;
		private readonly ITransactionFileTracker _tracker;

		public ChunkManagerForExecutor(ILogger logger, TFChunkManager manager, TFChunkDbConfig dbConfig,
			ITransactionFileTracker tracker) {
			_logger = logger;
			_manager = manager;
			_dbConfig = dbConfig;
			_tracker = tracker;
		}

		public IChunkWriterForExecutor<TStreamId, ILogRecord> CreateChunkWriter(
			IChunkReaderForExecutor<TStreamId, ILogRecord> sourceChunk) {

			return new ChunkWriterForExecutor<TStreamId>(_logger, this, _dbConfig, sourceChunk, _tracker);
		}

		public IChunkReaderForExecutor<TStreamId, ILogRecord> GetChunkReaderFor(long position) {
			var tfChunk = _manager.GetChunkFor(position);
			return new ChunkReaderForExecutor<TStreamId>(tfChunk, _tracker);
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
