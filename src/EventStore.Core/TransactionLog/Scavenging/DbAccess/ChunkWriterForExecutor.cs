using System;
using System.Collections.Generic;
using System.IO;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog.Scavenging {
	public class ChunkWriterForExecutor : IChunkWriterForExecutor<string, LogRecord> {
		const int BatchLength = 2000;
		private readonly ChunkManagerForExecutor _manager;
		private readonly TFChunk _outputChunk;
		private readonly List<List<PosMap>> _posMapss;
		private int _lastFlushedPage = -1;
		private long _fileSize = 0;

		public ChunkWriterForExecutor(
			ChunkManagerForExecutor manager,
			TFChunkDbConfig dbConfig,
			IChunkReaderForExecutor<string, LogRecord> sourceChunk) {

			_manager = manager;

			_posMapss = new List<List<PosMap>>(capacity: BatchLength) {
				new List<PosMap>(capacity: BatchLength)
			};

			// from TFChunkScavenger.ScavengeChunk
			FileName = Path.Combine(dbConfig.Path, Guid.NewGuid() + ".scavenge.tmp");
			_outputChunk = TFChunk.CreateNew(
				filename: FileName,
				chunkSize: dbConfig.ChunkSize,
				chunkStartNumber: sourceChunk.ChunkStartNumber,
				chunkEndNumber: sourceChunk.ChunkEndNumber,
				isScavenged: true,
				inMem: dbConfig.InMemDb,
				unbuffered: dbConfig.Unbuffered,
				writethrough: dbConfig.WriteThrough,
				initialReaderCount: dbConfig.InitialReaderCount,
				reduceFileCachePressure: dbConfig.ReduceFileCachePressure);
		}

		public string FileName { get; }

		public void WriteRecord(RecordForExecutor<string, LogRecord> record) {
			var posMap = TFChunkScavenger.WriteRecord(_outputChunk, record.Record);

			// add the posmap in memory so we can write it when we complete
			var lastBatch = _posMapss[_posMapss.Count - 1];
			if (lastBatch.Count >= BatchLength) {
				lastBatch = new List<PosMap>(capacity: BatchLength);
				_posMapss.Add(lastBatch);
			}

			lastBatch.Add(posMap);

			// occasionally flush the chunk. based on TFChunkScavenger.ScavengeChunk
			var currentPage = _outputChunk.RawWriterPosition / 4046;
			if (currentPage - _lastFlushedPage > TFChunkScavenger.FlushPageInterval) {
				_outputChunk.Flush();
				_lastFlushedPage = currentPage;
			}

			_fileSize += record.Length + 2 * sizeof(int);
		}

		public void Complete(out string newFileName, out long newFileSize) {
			// write posmap
			var posMapCount = 0;
			foreach (var list in _posMapss)
				posMapCount += list.Count;

			var unifiedPosMap = new List<PosMap>(capacity: posMapCount);
			foreach (var list in _posMapss)
				unifiedPosMap.AddRange(list);

			_outputChunk.CompleteScavenge(unifiedPosMap);

			_manager.SwitchChunk(chunk: _outputChunk, out newFileName);

			_fileSize += posMapCount * PosMap.FullSize + ChunkHeader.Size + ChunkFooter.Size;
			if (_outputChunk.ChunkHeader.Version >= (byte)TFChunk.ChunkVersions.Aligned)
				_fileSize = TFChunk.GetAlignedSize((int)_fileSize);
			newFileSize = _fileSize;
		}

		// tbh not sure why this distinction is important
		public void Abort(bool deleteImmediately) {
			if (deleteImmediately) {
				_outputChunk.Dispose();
				TFChunkScavenger.DeleteTempChunk(FileName, TFChunkScavenger.MaxRetryCount);
			} else {
				_outputChunk.MarkForDeletion();
			}
		}
	}
}
