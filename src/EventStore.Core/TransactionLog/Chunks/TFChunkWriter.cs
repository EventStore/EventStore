﻿using System;
using EventStore.Common.Utils;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.LogRecords;
using Serilog;
using Serilog.Events;

namespace EventStore.Core.TransactionLog.Chunks {
	public class TFChunkWriter : ITransactionFileWriter {
		public long Position => _writerCheckpoint.ReadNonFlushed();
		public long FlushedPosition => _writerCheckpoint.Read();

		public TFChunk.TFChunk CurrentChunk {
			get { return _currentChunk; }
		}

		public bool NeedsNewChunk =>
			CurrentChunk == null || // new database
			CurrentChunk.IsReadOnly; // database is at a chunk boundary

		private readonly TFChunkDb _db;
		private readonly ICheckpoint _writerCheckpoint;
		private long _nextRecordPosition;
		private bool _inTransaction;

		private TFChunk.TFChunk _currentChunk;

		private const int MaxChunkNumber = TFChunkManager.MaxChunksCount;
		private const int MaxChunkNumberError = MaxChunkNumber - 25_000;
		private const int MaxChunkNumberWarning = MaxChunkNumber - 50_000;

		private static readonly ILogger Log = Serilog.Log.ForContext<TFChunkWriter>();

		public TFChunkWriter(TFChunkDb db) {
			Ensure.NotNull(db, "db");

			_db = db;
			_writerCheckpoint = db.Config.WriterCheckpoint;
			_nextRecordPosition = _writerCheckpoint.Read();

			if (db.Manager.ChunksCount == 0) {
				// new database
				_currentChunk = null;
			} else if (!db.Manager.TryGetChunkFor(_nextRecordPosition, out _currentChunk)) {
				// we may have been at a chunk boundary and the new chunk wasn't yet created
				if (!db.Manager.TryGetChunkFor(_nextRecordPosition - 1, out _currentChunk))
					throw new Exception($"Failed to get chunk for log position: {_nextRecordPosition}");
			}
		}

		public void Open() {
			// DO NOTHING
		}

		public bool CanWrite(int numBytes) {
			return _currentChunk.ChunkHeader.ChunkEndPosition - _nextRecordPosition >= numBytes;
		}

		public bool Write(ILogRecord record, out long newPos) {
			OpenTransaction();

			if (!TryWriteToTransaction(record, out newPos)) {
				CompleteChunkInTransaction();
				AddNewChunk();
				CommitTransaction();
				Flush();
				newPos = _nextRecordPosition;
				return false;
			}

			CommitTransaction();
			return true;
		}

		public void OpenTransaction() {
			if (_inTransaction)
				throw new InvalidOperationException("Attempted to open a new transaction while there was an ongoing transaction.");

			_inTransaction = true;
		}

		public void WriteToTransaction(ILogRecord record, out long newPos) {
			if (!TryWriteToTransaction(record, out newPos))
				throw new InvalidOperationException("The transaction does not fit in the current chunk.");
		}

		public bool TryWriteToTransaction(ILogRecord record, out long newPos) {
			var result = _currentChunk.TryAppend(record);
			if (!result.Success) {
				newPos = default;
				return false;
			}

			_nextRecordPosition = result.NewPosition + _currentChunk.ChunkHeader.ChunkStartPosition;
			newPos = _nextRecordPosition;
			return true;
		}

		public void CommitTransaction() {
			if (!_inTransaction)
				throw new InvalidOperationException("Attempted to commit a transaction while there was no ongoing transaction.");

			_inTransaction = false;
			_writerCheckpoint.Write(_nextRecordPosition);
		}

		public bool HasOpenTransaction() => _inTransaction;

		public void AddNewChunk(ChunkHeader chunkHeader = null, ReadOnlyMemory<byte> transformHeader = default, int? chunkSize = null) {
			var nextChunkNumber = _currentChunk?.ChunkHeader.ChunkEndNumber + 1 ?? 0;
			VerifyChunkNumberLimits(nextChunkNumber);

			if (chunkHeader == null)
				_currentChunk = _db.Manager.AddNewChunk();
			else
				_currentChunk = _db.Manager.AddNewChunk(chunkHeader, transformHeader, chunkSize!.Value);
		}

		private void CompleteChunkInTransaction() {
			_currentChunk.Complete();
			_nextRecordPosition = _currentChunk.ChunkHeader.ChunkEndPosition;
		}

		public void CompleteChunk() {
			OpenTransaction();
			CompleteChunkInTransaction();
			CommitTransaction();
			Flush();
		}

		private void CompleteReplicatedRawChunkInTransaction(TFChunk.TFChunk rawChunk) {
			rawChunk.CompleteRaw();
			_currentChunk = _db.Manager.SwitchChunk(rawChunk, verifyHash: true, removeChunksWithGreaterNumbers: true);

			_nextRecordPosition = rawChunk.ChunkHeader.ChunkEndPosition;
		}

		public void CompleteReplicatedRawChunk(TFChunk.TFChunk rawChunk) {
			OpenTransaction();
			CompleteReplicatedRawChunkInTransaction(rawChunk);
			CommitTransaction();
			Flush();
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
			_writerCheckpoint.Flush();
		}
	}
}
