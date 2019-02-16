using System;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Exceptions;
using EventStore.Core.TransactionLog.Checkpoint;

namespace EventStore.Core.TransactionLog.Chunks {
	public class TFChunkReader : ITransactionFileReader {
		internal static long CachedReads;
		internal static long NotCachedReads;

		public const int MaxRetries = 20;

		public long CurrentPosition {
			get { return _curPos; }
		}

		private readonly TFChunkDb _db;
		private readonly ICheckpoint _writerCheckpoint;
		private long _curPos;
		private bool _optimizeReadSideCache;
		private readonly TFChunkReaderExistsAtOptimizer _existsAtOptimizer;

		public TFChunkReader(TFChunkDb db, ICheckpoint writerCheckpoint, long initialPosition = 0,
			bool optimizeReadSideCache = false) {
			Ensure.NotNull(db, "dbConfig");
			Ensure.NotNull(writerCheckpoint, "writerCheckpoint");
			Ensure.Nonnegative(initialPosition, "initialPosition");

			_db = db;
			_writerCheckpoint = writerCheckpoint;
			_curPos = initialPosition;

			_optimizeReadSideCache = optimizeReadSideCache;
			if (_optimizeReadSideCache)
				_existsAtOptimizer = TFChunkReaderExistsAtOptimizer.Instance;
		}

		public void Reposition(long position) {
			_curPos = position;
		}

		public SeqReadResult TryReadNext() {
			return TryReadNextInternal(0);
		}

		private SeqReadResult TryReadNextInternal(int retries) {
			while (true) {
				var pos = _curPos;
				var writerChk = _writerCheckpoint.Read();
				if (pos >= writerChk)
					return SeqReadResult.Failure;

				var chunk = _db.Manager.GetChunkFor(pos);
				RecordReadResult result;
				try {
					result = chunk.TryReadClosestForward(chunk.ChunkHeader.GetLocalLogPosition(pos));
					CountRead(chunk.IsCached);
				} catch (FileBeingDeletedException) {
					if (retries > MaxRetries)
						throw new Exception(
							string.Format(
								"Got a file that was being deleted {0} times from TFChunkDb, likely a bug there.",
								MaxRetries));
					return TryReadNextInternal(retries + 1);
				}

				if (result.Success) {
					_curPos = chunk.ChunkHeader.ChunkStartPosition + result.NextPosition;
					var postPos =
						result.LogRecord.GetNextLogPosition(result.LogRecord.LogPosition, result.RecordLength);
					var eof = postPos == writerChk;
					return new SeqReadResult(
						true, eof, result.LogRecord, result.RecordLength, result.LogRecord.LogPosition, postPos);
				}

				// we are the end of chunk
				_curPos = chunk.ChunkHeader.ChunkEndPosition; // the start of next physical chunk
			}
		}

		public SeqReadResult TryReadPrev() {
			return TryReadPrevInternal(0);
		}

		private SeqReadResult TryReadPrevInternal(int retries) {
			while (true) {
				var pos = _curPos;
				var writerChk = _writerCheckpoint.Read();
				// we allow == writerChk, that means read the very last record
				if (pos > writerChk)
					throw new Exception(string.Format(
						"Requested position {0} is greater than writer checkpoint {1} when requesting to read previous record from TF.",
						pos, writerChk));
				if (pos <= 0)
					return SeqReadResult.Failure;

				var chunk = _db.Manager.GetChunkFor(pos);
				bool readLast = false;
				if (pos == chunk.ChunkHeader.ChunkStartPosition) {
					// we are exactly at the boundary of physical chunks
					// so we switch to previous chunk and request TryReadLast
					readLast = true;
					chunk = _db.Manager.GetChunkFor(pos - 1);
				}

				RecordReadResult result;
				try {
					result = readLast
						? chunk.TryReadLast()
						: chunk.TryReadClosestBackward(chunk.ChunkHeader.GetLocalLogPosition(pos));
					CountRead(chunk.IsCached);
				} catch (FileBeingDeletedException) {
					if (retries > MaxRetries)
						throw new Exception(string.Format(
							"Got a file that was being deleted {0} times from TFChunkDb, likely a bug there.",
							MaxRetries));
					return TryReadPrevInternal(retries + 1);
				}

				if (result.Success) {
					_curPos = chunk.ChunkHeader.ChunkStartPosition + result.NextPosition;
					var postPos =
						result.LogRecord.GetNextLogPosition(result.LogRecord.LogPosition, result.RecordLength);
					var eof = postPos == writerChk;
					var res = new SeqReadResult(true, eof, result.LogRecord, result.RecordLength,
						result.LogRecord.LogPosition, postPos);
					return res;
				}

				// we are the beginning of chunk, so need to switch to previous one
				// to do that we set cur position to the exact boundary position between current and previous chunk,
				// this will be handled correctly on next iteration
				_curPos = chunk.ChunkHeader.ChunkStartPosition;
			}
		}

		public RecordReadResult TryReadAt(long position) {
			return TryReadAtInternal(position, 0);
		}

		private RecordReadResult TryReadAtInternal(long position, int retries) {
			var writerChk = _writerCheckpoint.Read();
			if (position >= writerChk)
				return RecordReadResult.Failure;

			var chunk = _db.Manager.GetChunkFor(position);
			try {
				CountRead(chunk.IsCached);
				return chunk.TryReadAt(chunk.ChunkHeader.GetLocalLogPosition(position));
			} catch (FileBeingDeletedException) {
				if (retries > MaxRetries)
					throw new FileBeingDeletedException(
						"Been told the file was deleted > MaxRetries times. Probably a problem in db.");
				return TryReadAtInternal(position, retries + 1);
			}
		}

		public bool ExistsAt(long position) {
			return ExistsAtInternal(position, 0);
		}

		private bool ExistsAtInternal(long position, int retries) {
			var writerChk = _writerCheckpoint.Read();
			if (position >= writerChk)
				return false;

			var chunk = _db.Manager.GetChunkFor(position);
			try {
				CountRead(chunk.IsCached);
				if (_optimizeReadSideCache)
					_existsAtOptimizer.Optimize(chunk);
				return chunk.ExistsAt(chunk.ChunkHeader.GetLocalLogPosition(position));
			} catch (FileBeingDeletedException) {
				if (retries > MaxRetries)
					throw new FileBeingDeletedException(
						"Been told the file was deleted > MaxRetries times. Probably a problem in db.");
				return ExistsAtInternal(position, retries + 1);
			}
		}

		private static void CountRead(bool isCached) {
			if (isCached)
				Interlocked.Increment(ref CachedReads);
			else
				Interlocked.Increment(ref NotCachedReads);
		}
	}
}
