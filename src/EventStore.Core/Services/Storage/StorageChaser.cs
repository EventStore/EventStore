using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Monitoring.Stats;
using EventStore.Core.Services.Storage.EpochManager;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.Services.Histograms;
using EventStore.Core.Util;
using System.Threading.Tasks;

namespace EventStore.Core.Services.Storage {
	public class StorageChaser : IMonitoredQueue,
		IHandle<SystemMessage.SystemInit>,
		IHandle<SystemMessage.SystemStart>,
		IHandle<SystemMessage.BecomeShuttingDown> {
		private static readonly ILogger Log = LogManager.GetLoggerFor<StorageChaser>();

		private static readonly int TicksPerMs = (int)(Stopwatch.Frequency / 1000);
		private static readonly int MinFlushDelay = 2 * TicksPerMs;
		private static readonly ManualResetEventSlim FlushSignal = new ManualResetEventSlim(false, 1);
		private static readonly TimeSpan FlushWaitTimeout = TimeSpan.FromMilliseconds(10);

		public string Name => _queueStats.Name;

		private readonly IPublisher _masterBus;
		private readonly ICheckpoint _writerCheckpoint;
		private readonly ITransactionFileChaser _chaser;
		private readonly IIndexCommitterService _indexCommitterService;
		private readonly IEpochManager _epochManager;
		private Thread _thread;
		private volatile bool _stop;
		private volatile bool _systemStarted;

		private readonly QueueStatsCollector _queueStats = new QueueStatsCollector("Storage Chaser");

		private readonly Stopwatch _watch = Stopwatch.StartNew();
		private long _flushDelay;
		private long _lastFlush;

		private readonly List<PrepareLogRecord> _transaction = new List<PrepareLogRecord>();
		private bool _commitsAfterEof;
		private const string ChaserWaitHistogram = "chaser-wait";
		private const string ChaserFlushHistogram = "chaser-flush";

		private readonly TaskCompletionSource<object> _tcs = new TaskCompletionSource<object>();

		public Task Task {
			get { return _tcs.Task; }
		}

		public StorageChaser(IPublisher masterBus,
			ICheckpoint writerCheckpoint,
			ITransactionFileChaser chaser,
			IIndexCommitterService indexCommitterService,
			IEpochManager epochManager) {
			Ensure.NotNull(masterBus, "masterBus");
			Ensure.NotNull(writerCheckpoint, "writerCheckpoint");
			Ensure.NotNull(chaser, "chaser");
			Ensure.NotNull(indexCommitterService, "indexCommitterService");
			Ensure.NotNull(epochManager, "epochManager");

			_masterBus = masterBus;
			_writerCheckpoint = writerCheckpoint;
			_chaser = chaser;
			_indexCommitterService = indexCommitterService;
			_epochManager = epochManager;

			_flushDelay = 0;
			_lastFlush = _watch.ElapsedTicks;
		}

		public void Handle(SystemMessage.SystemInit message) {
			_thread = new Thread(ChaseTransactionLog);
			_thread.IsBackground = true;
			_thread.Name = Name;
			_thread.Start();
		}

		public void Handle(SystemMessage.SystemStart message) {
			_systemStarted = true;
		}

		private void ChaseTransactionLog() {
			try {
				_queueStats.Start();
				QueueMonitor.Default.Register(this);

				_writerCheckpoint.Flushed += OnWriterFlushed;

				_chaser.Open();

				// We rebuild index till the chaser position, because
				// everything else will be done by chaser as during replication
				// with no concurrency issues with writer, as writer before jumping
				// into master-mode and accepting writes will wait till chaser caught up.
				_indexCommitterService.Init(_chaser.Checkpoint.Read());
				_masterBus.Publish(new SystemMessage.ServiceInitialized("StorageChaser"));

				while (!_stop) {
					if (_systemStarted)
						ChaserIteration();
					else
						Thread.Sleep(1);
				}
			} catch (Exception exc) {
				Log.FatalException(exc, "Error in StorageChaser. Terminating...");
				_queueStats.EnterIdle();
				_queueStats.ProcessingStarted<FaultedChaserState>(0);
				_tcs.TrySetException(exc);
				Application.Exit(ExitCode.Error, "Error in StorageChaser. Terminating...\nError: " + exc.Message);
				while (!_stop) {
					Thread.Sleep(100);
				}

				_queueStats.ProcessingEnded(0);
			} finally {
				_queueStats.Stop();
				QueueMonitor.Default.Unregister(this);
			}

			_writerCheckpoint.Flushed -= OnWriterFlushed;
			_chaser.Close();
			_masterBus.Publish(new SystemMessage.ServiceShutdown(Name));
		}

		private void OnWriterFlushed(long obj) {
			FlushSignal.Set();
		}

		private void ChaserIteration() {
			_queueStats.EnterBusy();

			FlushSignal.Reset(); // Reset the flush signal just before a read to reduce pointless reads from [flush flush read] patterns.

			var result = _chaser.TryReadNext();

			if (result.Success) {
				_queueStats.ProcessingStarted(result.LogRecord.GetType(), 0);
				ProcessLogRecord(result);
				_queueStats.ProcessingEnded(1);
			}

			var start = _watch.ElapsedTicks;
			if (!result.Success || start - _lastFlush >= _flushDelay + MinFlushDelay) {
				_queueStats.ProcessingStarted<ChaserCheckpointFlush>(0);
				var startflush = _watch.ElapsedTicks;
				_chaser.Flush();
				HistogramService.SetValue(ChaserFlushHistogram,
					(long)((((double)_watch.ElapsedTicks - startflush) / Stopwatch.Frequency) * 1000000000));
				_queueStats.ProcessingEnded(1);

				var end = _watch.ElapsedTicks;
				_flushDelay = end - start;
				_lastFlush = end;
			}

			if (!result.Success) {
				_queueStats.EnterIdle();
				var startwait = _watch.ElapsedTicks;
				FlushSignal.Wait(FlushWaitTimeout);
				HistogramService.SetValue(ChaserWaitHistogram,
					(long)((((double)_watch.ElapsedTicks - startwait) / Stopwatch.Frequency) * 1000000000));
			}
		}

		private void ProcessLogRecord(SeqReadResult result) {
			switch (result.LogRecord.RecordType) {
				case LogRecordType.Prepare: {
					var record = (PrepareLogRecord)result.LogRecord;
					ProcessPrepareRecord(record, result.RecordPostPosition);
					break;
				}
				case LogRecordType.Commit: {
					_commitsAfterEof = !result.Eof;
					var record = (CommitLogRecord)result.LogRecord;
					ProcessCommitRecord(record, result.RecordPostPosition);
					break;
				}
				case LogRecordType.System: {
					var record = (SystemLogRecord)result.LogRecord;
					ProcessSystemRecord(record);
					break;
				}
				default:
					throw new ArgumentOutOfRangeException();
			}

			if (result.Eof && result.LogRecord.RecordType != LogRecordType.Commit && _commitsAfterEof) {
				_commitsAfterEof = false;
				_masterBus.Publish(new StorageMessage.TfEofAtNonCommitRecord());
			}
		}

		private void ProcessPrepareRecord(PrepareLogRecord record, long postPosition) {
			if (_transaction.Count > 0 && _transaction[0].TransactionPosition != record.TransactionPosition)
				CommitPendingTransaction(_transaction, postPosition);

			if (record.Flags.HasAnyOf(PrepareFlags.IsCommitted)) {
				if (record.Flags.HasAnyOf(PrepareFlags.Data | PrepareFlags.StreamDelete))
					_transaction.Add(record);

				if (record.Flags.HasAnyOf(PrepareFlags.TransactionEnd)) {
					CommitPendingTransaction(_transaction, postPosition);

					long firstEventNumber;
					long lastEventNumber;
					if (record.Flags.HasAnyOf(PrepareFlags.Data)) {
						firstEventNumber = record.ExpectedVersion + 1 - record.TransactionOffset;
						lastEventNumber = record.ExpectedVersion + 1;
					} else {
						firstEventNumber = record.ExpectedVersion + 1;
						lastEventNumber = record.ExpectedVersion;
					}

					_masterBus.Publish(new StorageMessage.CommitAck(record.CorrelationId,
						record.LogPosition,
						record.TransactionPosition,
						firstEventNumber,
						lastEventNumber,
						true));
				}
			} else if (record.Flags.HasAnyOf(PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd)) {
				_masterBus.Publish(
					new StorageMessage.PrepareAck(record.CorrelationId, record.LogPosition, record.Flags));
			}
		}

		private void ProcessCommitRecord(CommitLogRecord record, long postPosition) {
			CommitPendingTransaction(_transaction, postPosition);

			var firstEventNumber = record.FirstEventNumber;
			var lastEventNumber = _indexCommitterService.GetCommitLastEventNumber(record);
			_indexCommitterService.AddPendingCommit(record, postPosition);
			if (lastEventNumber == EventNumber.Invalid)
				lastEventNumber = record.FirstEventNumber - 1;
			_masterBus.Publish(new StorageMessage.CommitAck(record.CorrelationId, record.LogPosition,
				record.TransactionPosition, firstEventNumber, lastEventNumber, true));
		}

		private void ProcessSystemRecord(SystemLogRecord record) {
			CommitPendingTransaction(_transaction, record.LogPosition);

			if (record.SystemRecordType == SystemRecordType.Epoch) {
				// Epoch record is written to TF, but possibly is not added to EpochManager
				// as we could be in Slave\Clone mode. We try to add epoch to EpochManager
				// every time we encounter EpochRecord while chasing. SetLastEpoch call is idempotent,
				// but does integrity checks.
				var epoch = record.GetEpochRecord();
				_epochManager.SetLastEpoch(epoch);
			}
		}

		private void CommitPendingTransaction(List<PrepareLogRecord> transaction, long postPosition) {
			if (transaction.Count > 0) {
				_indexCommitterService.AddPendingPrepare(transaction.ToArray(), postPosition);
				_transaction.Clear();
			}
		}

		public void Handle(SystemMessage.BecomeShuttingDown message) {
			_stop = true;
		}

		public QueueStats GetStatistics() {
			return _queueStats.GetStatistics(0);
		}

		private class ChaserCheckpointFlush {
		}

		private class FaultedChaserState {
		}
	}
}
