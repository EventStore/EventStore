using System;
using EventStore.Common.Utils;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog.Chunks {
	public class TFChunkWriter : ITransactionFileWriter {
		public ICheckpoint Checkpoint {
			get { return _writerCheckpoint; }
		}

		public TFChunk.TFChunk CurrentChunk {
			get { return _currentChunk; }
		}

		private readonly TFChunkDb _db;
		private readonly ICheckpoint _writerCheckpoint;

		private TFChunk.TFChunk _currentChunk;

		public TFChunkWriter(TFChunkDb db) {
			Ensure.NotNull(db, "db");

			_db = db;
			_writerCheckpoint = db.Config.WriterCheckpoint;
			_currentChunk = db.Manager.GetChunkFor(_writerCheckpoint.Read());
			if (_currentChunk == null)
				throw new InvalidOperationException("No chunk given for existing position.");
		}

		public void Open() {
			// DO NOTHING
		}

		public bool Write(LogRecord record, out long newPos) {
			var result = _currentChunk.TryAppend(record);
			if (result.Success)
				_writerCheckpoint.Write(result.NewPosition + _currentChunk.ChunkHeader.ChunkStartPosition);
			else
				CompleteChunk(); // complete updates checkpoint internally
			newPos = _writerCheckpoint.ReadNonFlushed();
			return result.Success;
		}

		public void CompleteChunk() {
			var chunk = _currentChunk;
			_currentChunk = null; // in case creation of new chunk fails, we shouldn't use completed chunk for write

			chunk.Complete();

			_writerCheckpoint.Write(chunk.ChunkHeader.ChunkEndPosition);
			_writerCheckpoint.Flush();

			_currentChunk = _db.Manager.AddNewChunk();
		}

		public void CompleteReplicatedRawChunk(TFChunk.TFChunk rawChunk) {
			_currentChunk = null; // in case creation of new chunk fails, we shouldn't use completed chunk for write

			rawChunk.CompleteRaw();
			_db.Manager.SwitchChunk(rawChunk, verifyHash: true, removeChunksWithGreaterNumbers: true);

			_writerCheckpoint.Write(rawChunk.ChunkHeader.ChunkEndPosition);
			_writerCheckpoint.Flush();

			_currentChunk = _db.Manager.AddNewChunk();
		}

		public void Dispose() {
			Close();
		}

		public void Close() {
			Flush();
		}

		public void Flush() {
			if (_currentChunk == null) // the last chunk allocation failed
				return;
			_currentChunk.Flush();
			_writerCheckpoint.Flush();
		}
	}
}
