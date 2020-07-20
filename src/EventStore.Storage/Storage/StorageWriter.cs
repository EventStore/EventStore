using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Common.Utils;
using EventStore.Core.Services.Storage.EpochManager;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.LogRecords;

using ILogger = Serilog.ILogger;

namespace EventStore.Core.Services.Storage {
	public abstract class StorageWriter {
		private static readonly ILogger Log = Serilog.Log.ForContext<StorageWriter>();

		protected static readonly int TicksPerMs = (int)(Stopwatch.Frequency / 1000);
		protected static readonly TimeSpan WaitForChaserSingleIterationTimeout = TimeSpan.FromMilliseconds(200);

		protected readonly TFChunkDb Db;
		protected readonly TFChunkWriter Writer;
		protected readonly IEpochManager EpochManager;
		private readonly Action<string, long> _histogramSetValue;

		private readonly Stopwatch _watch = Stopwatch.StartNew();
		private readonly double _minFlushDelay;
		private long _lastFlushDelay;
		private long _lastFlushTimestamp;

		protected int FlushMessagesInQueue;
		
		protected bool BlockWriter = false;

		private const int LastStatsCount = 1024;
		private readonly long[] _lastFlushDelays = new long[LastStatsCount];
		private readonly long[] _lastFlushSizes = new long[LastStatsCount];
		private int _statIndex;
		private int _statCount;
		private long _sumFlushDelay;
		private long _sumFlushSize;
		private long _lastFlushSize;
		private long _maxFlushSize;
		private long _maxFlushDelay;
		private const string _writerFlushHistogram = "writer-flush";
		protected readonly List<Task> _tasks = new List<Task>();

		public IEnumerable<Task> Tasks {
			get { return _tasks; }
		}

		protected StorageWriter(
			TimeSpan minFlushDelay,
			TFChunkDb db,
			TFChunkWriter writer,
			IEpochManager epochManager,
			Action<string,long> histogramSetValue) {
			Ensure.NotNull(db, "db");
			Ensure.NotNull(writer, "writer");
			Ensure.NotNull(epochManager, "epochManager");
			
			Db = db;
			EpochManager = epochManager;
			_histogramSetValue = histogramSetValue;

			_minFlushDelay = minFlushDelay.TotalMilliseconds * TicksPerMs;
			_lastFlushDelay = 0;
			_lastFlushTimestamp = _watch.ElapsedTicks;

			Writer = writer;
			Writer.Open();
		}
		
		
		protected WriteResult WritePrepareWithRetry(PrepareLogRecord prepare) {
			long writtenPos = prepare.LogPosition;
			long newPos;
			PrepareLogRecord record = prepare;
			if (!Writer.Write(prepare, out newPos)) {
				var transactionPos = prepare.TransactionPosition == prepare.LogPosition
					? newPos
					: prepare.TransactionPosition;
				record = new PrepareLogRecord(newPos,
					prepare.CorrelationId,
					prepare.EventId,
					transactionPos,
					prepare.TransactionOffset,
					prepare.EventStreamId,
					prepare.ExpectedVersion,
					prepare.TimeStamp,
					prepare.Flags,
					prepare.EventType,
					prepare.Data,
					prepare.Metadata);
				writtenPos = newPos;
				if (!Writer.Write(record, out newPos)) {
					throw new Exception(
						string.Format("Second write try failed when first writing prepare at {0}, then at {1}.",
							prepare.LogPosition,
							writtenPos));
				}
			}

			return new WriteResult(writtenPos, newPos, record);
		}

		protected CommitLogRecord WriteCommitWithRetry(CommitLogRecord commit) {
			long newPos;
			if (!Writer.Write(commit, out newPos)) {
				var transactionPos = commit.TransactionPosition == commit.LogPosition
					? newPos
					: commit.TransactionPosition;
				var record = new CommitLogRecord(newPos,
					commit.CorrelationId,
					transactionPos,
					commit.TimeStamp,
					commit.FirstEventNumber);
				long writtenPos = newPos;
				if (!Writer.Write(record, out newPos)) {
					throw new Exception(
						string.Format("Second write try failed when first writing commit at {0}, then at {1}.",
							commit.LogPosition,
							writtenPos));
				}

				return record;
			}

			return commit;
		}

		protected bool Flush( Action onFlush, bool force = false) {
			var start = _watch.ElapsedTicks;
			if (force || FlushMessagesInQueue == 0 || start - _lastFlushTimestamp >= _lastFlushDelay + _minFlushDelay) {
				var flushSize = Writer.Checkpoint.ReadNonFlushed() - Writer.Checkpoint.Read();

				Writer.Flush();
				_histogramSetValue(_writerFlushHistogram,
					(long)((((double)_watch.ElapsedTicks - start) / Stopwatch.Frequency) * 1000000000));
				var end = _watch.ElapsedTicks;
				var flushDelay = end - start;
				Interlocked.Exchange(ref _lastFlushDelay, flushDelay);
				Interlocked.Exchange(ref _lastFlushSize, flushSize);
				_lastFlushTimestamp = end;

				if (_statCount >= LastStatsCount) {
					Interlocked.Add(ref _sumFlushSize, -_lastFlushSizes[_statIndex]);
					Interlocked.Add(ref _sumFlushDelay, -_lastFlushDelays[_statIndex]);
				} else {
					_statCount += 1;
				}

				_lastFlushSizes[_statIndex] = flushSize;
				_lastFlushDelays[_statIndex] = flushDelay;
				Interlocked.Add(ref _sumFlushSize, flushSize);
				Interlocked.Add(ref _sumFlushDelay, flushDelay);
				Interlocked.Exchange(ref _maxFlushSize, Math.Max(Interlocked.Read(ref _maxFlushSize), flushSize));
				Interlocked.Exchange(ref _maxFlushDelay, Math.Max(Interlocked.Read(ref _maxFlushDelay), flushDelay));
				_statIndex = (_statIndex + 1) & (LastStatsCount - 1);

				PurgeNotProcessedInfo();
				//todo: inject publish
				//Bus.Publish(new ReplicationTrackingMessage.WriterCheckpointFlushed());
				onFlush();
				return true;
			}

			return false;
		}

		protected abstract void PurgeNotProcessedInfo();

		protected struct WriteResult {
			public readonly long WrittenPos;
			public readonly long NewPos;
			public readonly PrepareLogRecord Prepare;

			public WriteResult(long writtenPos, long newPos, PrepareLogRecord prepare) {
				WrittenPos = writtenPos;
				NewPos = newPos;
				Prepare = prepare;
			}
		}
	}
}
