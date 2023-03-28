using System;
using EventStore.Common.Utils;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.LogRecords;
using Serilog;
using Serilog.Events;

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
		private long _writerPosition;

		private TFChunk.TFChunk _currentChunk;

		private const int MaxChunkNumber = TFChunkManager.MaxChunksCount;
		private const int MaxChunkNumberError = MaxChunkNumber - 25_000;
		private const int MaxChunkNumberWarning = MaxChunkNumber - 50_000;

		private static readonly ILogger Log = Serilog.Log.ForContext<TFChunkWriter>();

		public TFChunkWriter(TFChunkDb db) {
			Ensure.NotNull(db, "db");

			_db = db;
			_writerCheckpoint = db.Config.WriterCheckpoint;
			_writerPosition = _writerCheckpoint.Read();
			_currentChunk = db.Manager.GetChunkFor(_writerPosition);
			if (_currentChunk == null)
				throw new InvalidOperationException("No chunk given for existing position.");
		}

		public void Open() {
			// DO NOTHING
		}

		public bool Write(ILogRecord record, out long newPos) {
			var result = _currentChunk.TryAppend(record);
			if (result.Success)
				_writerPosition = result.NewPosition + _currentChunk.ChunkHeader.ChunkStartPosition;
			else
				CompleteChunk(); // complete updates checkpoint internally
			newPos = _writerPosition;
			return result.Success;
		}

		public void CompleteChunk() {
			var chunk = _currentChunk;
			_currentChunk = null; // in case creation of new chunk fails, we shouldn't use completed chunk for write

			chunk.Complete();

			_writerPosition = chunk.ChunkHeader.ChunkEndPosition;

			var nextChunkNumber = chunk.ChunkHeader.ChunkEndNumber + 1;
			VerifyChunkNumberLimits(nextChunkNumber);
			_currentChunk = _db.Manager.AddNewChunk();
		}

		public void CompleteReplicatedRawChunk(TFChunk.TFChunk rawChunk) {
			_currentChunk = null; // in case creation of new chunk fails, we shouldn't use completed chunk for write

			rawChunk.CompleteRaw();
			_db.Manager.SwitchChunk(rawChunk, verifyHash: true, removeChunksWithGreaterNumbers: true);

			_writerPosition = rawChunk.ChunkHeader.ChunkEndPosition;

			// we can safely flush the writer checkpoint here since data in a raw chunk is already committed to the log
			_writerCheckpoint.Write(_writerPosition);
			_writerCheckpoint.Flush();

			var nextChunkNumber = rawChunk.ChunkHeader.ChunkEndNumber + 1;
			VerifyChunkNumberLimits(nextChunkNumber);
			_currentChunk = _db.Manager.AddNewChunk();
		}

		private static void VerifyChunkNumberLimits(int chunkNumber) {
			switch (chunkNumber)
			{
				case >= MaxChunkNumber:
					throw new Exception($"Max chunk number limit reached: {MaxChunkNumber:N0}. Shutting down.");
				case < MaxChunkNumberWarning:
					break;
				default:
				{
					var level = chunkNumber >= MaxChunkNumberError ? LogEventLevel.Error : LogEventLevel.Warning;
					Log.Write(level, "You are approaching the max chunk number limit: {chunkNumber:N0} / {maxChunkNumber:N0}. " +
					                 "The server will shut down when the limit is reached!", chunkNumber, MaxChunkNumber);
					break;
				}
			}
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
			_writerCheckpoint.Write(_writerPosition);
			_writerCheckpoint.Flush();
		}
	}
}
