using System;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.Transforms;
using Serilog;

namespace EventStore.Core.TransactionLog.Scavenging {
	public class ChunkManagerForExecutor<TStreamId> : IChunkManagerForChunkExecutor<TStreamId, ILogRecord> {
		private readonly ILogger _logger;
		private readonly TFChunkManager _manager;
		private readonly TFChunkDbConfig _dbConfig;
		private readonly DbTransformManager _transformManager;

		public ChunkManagerForExecutor(ILogger logger, TFChunkManager manager, TFChunkDbConfig dbConfig, DbTransformManager transformManager) {
			_logger = logger;
			_manager = manager;
			_dbConfig = dbConfig;
			_transformManager = transformManager;
		}

		public IChunkWriterForExecutor<TStreamId, ILogRecord> CreateChunkWriter(
			IChunkReaderForExecutor<TStreamId, ILogRecord> sourceChunk) {

			return new ChunkWriterForExecutor<TStreamId>(_logger, this, _dbConfig, sourceChunk, _transformManager);
		}

		public IChunkReaderForExecutor<TStreamId, ILogRecord> GetChunkReaderFor(long position) {
			var tfChunk = _manager.GetChunkFor(position);
			return new ChunkReaderForExecutor<TStreamId>(tfChunk);
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
