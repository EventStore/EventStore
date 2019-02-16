using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Histograms;
using EventStore.Core.Services.Monitoring.Stats;
using EventStore.Core.Services.Storage.EpochManager;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.LogRecords;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EventStore.Core.Services.Storage {
	public class StorageWriterService : IHandle<SystemMessage.SystemInit>,
		IHandle<SystemMessage.StateChangeMessage>,
		IHandle<SystemMessage.WriteEpoch>,
		IHandle<SystemMessage.WaitForChaserToCatchUp>,
		IHandle<StorageMessage.WritePrepares>,
		IHandle<StorageMessage.WriteDelete>,
		IHandle<StorageMessage.WriteTransactionStart>,
		IHandle<StorageMessage.WriteTransactionData>,
		IHandle<StorageMessage.WriteTransactionPrepare>,
		IHandle<StorageMessage.WriteCommit>,
		IHandle<MonitoringMessage.InternalStatsRequest> {
		private static readonly ILogger Log = LogManager.GetLoggerFor<StorageWriterService>();

		protected static readonly int TicksPerMs = (int)(Stopwatch.Frequency / 1000);
		private static readonly TimeSpan WaitForChaserSingleIterationTimeout = TimeSpan.FromMilliseconds(200);

		protected readonly TFChunkDb Db;
		protected readonly TFChunkWriter Writer;
		private readonly IIndexWriter _indexWriter;
		protected readonly IEpochManager EpochManager;

		protected readonly IPublisher Bus;
		private readonly ISubscriber _subscribeToBus;
		protected readonly IQueuedHandler StorageWriterQueue;
		private readonly InMemoryBus _writerBus;

		private readonly Stopwatch _watch = Stopwatch.StartNew();
		private readonly double _minFlushDelay;
		private long _lastFlushDelay;
		private long _lastFlushTimestamp;

		protected int FlushMessagesInQueue;
		private VNodeState _vnodeState = VNodeState.Initializing;
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
		private readonly List<Task> _tasks = new List<Task>();

		public IEnumerable<Task> Tasks {
			get { return _tasks; }
		}

		public StorageWriterService(IPublisher bus,
			ISubscriber subscribeToBus,
			TimeSpan minFlushDelay,
			TFChunkDb db,
			TFChunkWriter writer,
			IIndexWriter indexWriter,
			IEpochManager epochManager) {
			Ensure.NotNull(bus, "bus");
			Ensure.NotNull(subscribeToBus, "subscribeToBus");
			Ensure.NotNull(db, "db");
			Ensure.NotNull(writer, "writer");
			Ensure.NotNull(indexWriter, "indexWriter");
			Ensure.NotNull(epochManager, "epochManager");

			Bus = bus;
			_subscribeToBus = subscribeToBus;
			Db = db;
			_indexWriter = indexWriter;
			EpochManager = epochManager;

			_minFlushDelay = minFlushDelay.TotalMilliseconds * TicksPerMs;
			_lastFlushDelay = 0;
			_lastFlushTimestamp = _watch.ElapsedTicks;

			Writer = writer;
			Writer.Open();

			_writerBus = new InMemoryBus("StorageWriterBus", watchSlowMsg: false);
			StorageWriterQueue = QueuedHandler.CreateQueuedHandler(new AdHocHandler<Message>(CommonHandle),
				"StorageWriterQueue",
				true,
				TimeSpan.FromMilliseconds(500));
			_tasks.Add(StorageWriterQueue.Start());

			SubscribeToMessage<SystemMessage.SystemInit>();
			SubscribeToMessage<SystemMessage.StateChangeMessage>();
			SubscribeToMessage<SystemMessage.WriteEpoch>();
			SubscribeToMessage<SystemMessage.WaitForChaserToCatchUp>();
			SubscribeToMessage<StorageMessage.WritePrepares>();
			SubscribeToMessage<StorageMessage.WriteDelete>();
			SubscribeToMessage<StorageMessage.WriteTransactionStart>();
			SubscribeToMessage<StorageMessage.WriteTransactionData>();
			SubscribeToMessage<StorageMessage.WriteTransactionPrepare>();
			SubscribeToMessage<StorageMessage.WriteCommit>();
		}

		protected void SubscribeToMessage<T>() where T : Message {
			_writerBus.Subscribe((IHandle<T>)this);
			_subscribeToBus.Subscribe(new AdHocHandler<Message>(EnqueueMessage).WidenFrom<T, Message>());
		}

		private void EnqueueMessage(Message message) {
			if (message is StorageMessage.IFlushableMessage)
				Interlocked.Increment(ref FlushMessagesInQueue);

			StorageWriterQueue.Publish(message);

			if (message is SystemMessage.BecomeShuttingDown)
				// we need to handle this message on main thread to stop StorageWriterQueue
			{
				StorageWriterQueue.Stop();
				BlockWriter = true;
				Bus.Publish(new SystemMessage.ServiceShutdown("StorageWriter"));
			}
		}

		private void CommonHandle(Message message) {
			if (BlockWriter && !(message is SystemMessage.StateChangeMessage)) {
				Log.Trace("Blocking message {message} in StorageWriterService. Message:", message.GetType().Name);
				Log.Trace("{message}", message);
				return;
			}

			if (_vnodeState != VNodeState.Master && message is StorageMessage.IMasterWriteMessage) {
				Log.Fatal("{message} appeared in StorageWriter during state {vnodeStrate}.", message.GetType().Name,
					_vnodeState);
				var msg = String.Format("{0} appeared in StorageWriter during state {1}.", message.GetType().Name,
					_vnodeState);
				Application.Exit(ExitCode.Error, msg);
				return;
			}

			try {
				_writerBus.Handle(message);
			} catch (Exception exc) {
				BlockWriter = true;
				Log.FatalException(exc, "Unexpected error in StorageWriterService. Terminating the process...");
				Application.Exit(ExitCode.Error,
					string.Format("Unexpected error in StorageWriterService: {0}", exc.Message));
			}
		}

		void IHandle<SystemMessage.SystemInit>.Handle(SystemMessage.SystemInit message) {
			Bus.Publish(new SystemMessage.ServiceInitialized("StorageWriter"));
		}

		public virtual void Handle(SystemMessage.StateChangeMessage message) {
			_vnodeState = message.State;

			switch (message.State) {
				case VNodeState.Master: {
					_indexWriter.Reset();
					EpochManager.WriteNewEpoch(); // forces flush
					break;
				}
				case VNodeState.ShuttingDown: {
					Writer.Close();
					break;
				}
			}
		}

		void IHandle<SystemMessage.WriteEpoch>.Handle(SystemMessage.WriteEpoch message) {
			if (_vnodeState == VNodeState.PreMaster)
				return;
			if (_vnodeState != VNodeState.Master)
				throw new Exception(string.Format("New Epoch request not in master state. State: {0}.", _vnodeState));
			EpochManager.WriteNewEpoch();
			PurgeNotProcessedInfo();
		}

		void IHandle<SystemMessage.WaitForChaserToCatchUp>.Handle(SystemMessage.WaitForChaserToCatchUp message) {
			// if we are in states, that doesn't need to wait for chaser, ignore
			if (_vnodeState != VNodeState.PreMaster && _vnodeState != VNodeState.PreReplica)
				throw new Exception(string.Format("{0} appeared in {1} state.", message.GetType().Name, _vnodeState));

			if (Writer.Checkpoint.Read() != Writer.Checkpoint.ReadNonFlushed())
				Writer.Flush();

			var sw = Stopwatch.StartNew();
			while (Db.Config.ChaserCheckpoint.Read() < Db.Config.WriterCheckpoint.Read() &&
			       sw.Elapsed < WaitForChaserSingleIterationTimeout) {
				Thread.Sleep(1);
			}

			if (Db.Config.ChaserCheckpoint.Read() == Db.Config.WriterCheckpoint.Read()) {
				Bus.Publish(new SystemMessage.ChaserCaughtUp(message.CorrelationId));
				return;
			}

			var totalTime = message.TotalTimeWasted + sw.Elapsed;
			if (totalTime < TimeSpan.FromSeconds(5) || (int)totalTime.TotalSeconds % 30 == 0) // too verbose otherwise
				Log.Debug("Still waiting for chaser to catch up already for {totalTime}...", totalTime);
			Bus.Publish(new SystemMessage.WaitForChaserToCatchUp(message.CorrelationId, totalTime));
		}

		void IHandle<StorageMessage.WritePrepares>.Handle(StorageMessage.WritePrepares msg) {
			Interlocked.Decrement(ref FlushMessagesInQueue);

			try {
				if (msg.LiveUntil < DateTime.UtcNow)
					return;

				string streamId = msg.EventStreamId;
				var commitCheck = _indexWriter.CheckCommit(streamId, msg.ExpectedVersion,
					msg.Events.Select(x => x.EventId));
				if (commitCheck.Decision != CommitDecision.Ok) {
					ActOnCommitCheckFailure(msg.Envelope, msg.CorrelationId, commitCheck);
					return;
				}

				var prepares = new List<PrepareLogRecord>();
				var logPosition = Writer.Checkpoint.ReadNonFlushed();
				if (msg.Events.Length > 0) {
					var transactionPosition = logPosition;
					for (int i = 0; i < msg.Events.Length; ++i) {
						var evnt = msg.Events[i];
						var flags = PrepareFlags.Data | PrepareFlags.IsCommitted;
						if (i == 0)
							flags |= PrepareFlags.TransactionBegin;
						if (i == msg.Events.Length - 1)
							flags |= PrepareFlags.TransactionEnd;
						if (evnt.IsJson)
							flags |= PrepareFlags.IsJson;

						// when IsCommitted ExpectedVersion is always explicit
						var expectedVersion = commitCheck.CurrentVersion + i;
						var res = WritePrepareWithRetry(
							LogRecord.Prepare(logPosition, msg.CorrelationId, evnt.EventId,
								transactionPosition, i, streamId,
								expectedVersion, flags, evnt.EventType, evnt.Data, evnt.Metadata));
						logPosition = res.NewPos;
						if (i == 0)
							transactionPosition = res.WrittenPos;
						// transaction position could be changed due to switching to new chunk
						prepares.Add(res.Prepare);
					}
				} else {
					WritePrepareWithRetry(
						LogRecord.Prepare(logPosition, msg.CorrelationId, Guid.NewGuid(), logPosition, -1,
							streamId, commitCheck.CurrentVersion,
							PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd | PrepareFlags.IsCommitted,
							null, Empty.ByteArray, Empty.ByteArray));
				}

				bool softUndeleteMetastream = SystemStreams.IsMetastream(streamId)
				                              && _indexWriter.IsSoftDeleted(SystemStreams.OriginalStreamOf(streamId));

				_indexWriter.PreCommit(prepares);

				if (commitCheck.IsSoftDeleted)
					SoftUndeleteStream(streamId, commitCheck.CurrentVersion + 1);
				if (softUndeleteMetastream)
					SoftUndeleteMetastream(streamId);
			} catch (Exception exc) {
				Log.ErrorException(exc, "Exception in writer.");
				throw;
			} finally {
				Flush();
			}
		}


		private void SoftUndeleteMetastream(string metastreamId) {
			var origStreamId = SystemStreams.OriginalStreamOf(metastreamId);
			var rawMetaInfo = _indexWriter.GetStreamRawMeta(origStreamId);
			SoftUndeleteStream(origStreamId, rawMetaInfo.MetaLastEventNumber, rawMetaInfo.RawMeta,
				recreateFrom: _indexWriter.GetStreamLastEventNumber(origStreamId) + 1);
		}

		private void SoftUndeleteStream(string streamId, long recreateFromEventNumber) {
			var rawInfo = _indexWriter.GetStreamRawMeta(streamId);
			SoftUndeleteStream(streamId, rawInfo.MetaLastEventNumber, rawInfo.RawMeta, recreateFromEventNumber);
		}

		private void SoftUndeleteStream(string streamId, long metaLastEventNumber, byte[] rawMeta, long recreateFrom) {
			byte[] modifiedMeta;
			if (!SoftUndeleteRawMeta(rawMeta, recreateFrom, out modifiedMeta))
				return;

			var logPosition = Writer.Checkpoint.ReadNonFlushed();
			var res = WritePrepareWithRetry(
				LogRecord.Prepare(logPosition, Guid.NewGuid(), Guid.NewGuid(), logPosition, 0,
					SystemStreams.MetastreamOf(streamId), metaLastEventNumber,
					PrepareFlags.SingleWrite | PrepareFlags.IsCommitted | PrepareFlags.IsJson,
					SystemEventTypes.StreamMetadata, modifiedMeta, Empty.ByteArray));

			_indexWriter.PreCommit(new[] {res.Prepare});
		}

		public bool SoftUndeleteRawMeta(byte[] rawMeta, long recreateFromEventNumber, out byte[] modifiedMeta) {
			try {
				var jobj = JObject.Parse(Encoding.UTF8.GetString(rawMeta));
				jobj[SystemMetadata.TruncateBefore] = recreateFromEventNumber;
				using (var memoryStream = new MemoryStream()) {
					using (var jsonWriter = new JsonTextWriter(new StreamWriter(memoryStream))) {
						jobj.WriteTo(jsonWriter);
					}

					modifiedMeta = memoryStream.ToArray();
					return true;
				}
			} catch (Exception) {
				modifiedMeta = null;
				return false;
			}
		}

		void IHandle<StorageMessage.WriteDelete>.Handle(StorageMessage.WriteDelete message) {
			Interlocked.Decrement(ref FlushMessagesInQueue);
			try {
				if (message.LiveUntil < DateTime.UtcNow)
					return;

				var eventId = Guid.NewGuid();

				var commitCheck = _indexWriter.CheckCommit(message.EventStreamId, message.ExpectedVersion,
					new[] {eventId});
				if (commitCheck.Decision != CommitDecision.Ok) {
					ActOnCommitCheckFailure(message.Envelope, message.CorrelationId, commitCheck);
					return;
				}

				if (message.HardDelete) {
					// HARD DELETE
					const long expectedVersion = EventNumber.DeletedStream - 1;
					var record = LogRecord.DeleteTombstone(Writer.Checkpoint.ReadNonFlushed(), message.CorrelationId,
						eventId, message.EventStreamId, expectedVersion, PrepareFlags.IsCommitted);
					var res = WritePrepareWithRetry(record);
					_indexWriter.PreCommit(new[] {res.Prepare});
				} else {
					// SOFT DELETE
					var metastreamId = SystemStreams.MetastreamOf(message.EventStreamId);
					var expectedVersion = _indexWriter.GetStreamLastEventNumber(metastreamId);
					var logPosition = Writer.Checkpoint.ReadNonFlushed();
					const PrepareFlags flags = PrepareFlags.SingleWrite | PrepareFlags.IsCommitted |
					                           PrepareFlags.IsJson;
					var data = new StreamMetadata(truncateBefore: EventNumber.DeletedStream).ToJsonBytes();
					var res = WritePrepareWithRetry(
						LogRecord.Prepare(logPosition, message.CorrelationId, eventId, logPosition, 0,
							metastreamId, expectedVersion, flags, SystemEventTypes.StreamMetadata,
							data, null));
					_indexWriter.PreCommit(new[] {res.Prepare});
				}
			} catch (Exception exc) {
				Log.ErrorException(exc, "Exception in writer.");
				throw;
			} finally {
				Flush();
			}
		}

		void IHandle<StorageMessage.WriteTransactionStart>.Handle(StorageMessage.WriteTransactionStart message) {
			Interlocked.Decrement(ref FlushMessagesInQueue);
			try {
				if (message.LiveUntil < DateTime.UtcNow)
					return;

				var record = LogRecord.TransactionBegin(Writer.Checkpoint.ReadNonFlushed(),
					message.CorrelationId,
					message.EventStreamId,
					message.ExpectedVersion);
				var res = WritePrepareWithRetry(record);

				// we update cache to avoid non-cached look-up on next TransactionWrite
				_indexWriter.UpdateTransactionInfo(res.WrittenPos, res.WrittenPos,
					new TransactionInfo(-1, message.EventStreamId));
			} catch (Exception exc) {
				Log.ErrorException(exc, "Exception in writer.");
				throw;
			} finally {
				Flush();
			}
		}

		void IHandle<StorageMessage.WriteTransactionData>.Handle(StorageMessage.WriteTransactionData message) {
			Interlocked.Decrement(ref FlushMessagesInQueue);
			try {
				var logPosition = Writer.Checkpoint.ReadNonFlushed();
				var transactionInfo = _indexWriter.GetTransactionInfo(Writer.Checkpoint.Read(), message.TransactionId);
				if (!CheckTransactionInfo(message.TransactionId, transactionInfo))
					return;

				if (message.Events.Length > 0) {
					long lastLogPosition = -1;
					for (int i = 0; i < message.Events.Length; ++i) {
						var evnt = message.Events[i];
						var record = LogRecord.TransactionWrite(logPosition,
							message.CorrelationId,
							evnt.EventId,
							message.TransactionId,
							transactionInfo.TransactionOffset + i + 1,
							transactionInfo.EventStreamId,
							evnt.EventType,
							evnt.Data,
							evnt.Metadata,
							evnt.IsJson);
						var res = WritePrepareWithRetry(record);
						logPosition = res.NewPos;
						lastLogPosition = res.WrittenPos;
					}

					var info = new TransactionInfo(transactionInfo.TransactionOffset + message.Events.Length,
						transactionInfo.EventStreamId);
					_indexWriter.UpdateTransactionInfo(message.TransactionId, lastLogPosition, info);
				}
			} catch (Exception exc) {
				Log.ErrorException(exc, "Exception in writer.");
				throw;
			} finally {
				Flush();
			}
		}

		void IHandle<StorageMessage.WriteTransactionPrepare>.Handle(StorageMessage.WriteTransactionPrepare message) {
			Interlocked.Decrement(ref FlushMessagesInQueue);
			try {
				if (message.LiveUntil < DateTime.UtcNow)
					return;

				var transactionInfo = _indexWriter.GetTransactionInfo(Writer.Checkpoint.Read(), message.TransactionId);
				if (!CheckTransactionInfo(message.TransactionId, transactionInfo))
					return;

				var record = LogRecord.TransactionEnd(Writer.Checkpoint.ReadNonFlushed(),
					message.CorrelationId,
					Guid.NewGuid(),
					message.TransactionId,
					transactionInfo.EventStreamId);
				WritePrepareWithRetry(record);
			} catch (Exception exc) {
				Log.ErrorException(exc, "Exception in writer.");
				throw;
			} finally {
				Flush();
			}
		}

		private static bool CheckTransactionInfo(long transactionId, TransactionInfo transactionInfo) {
			if (transactionInfo.TransactionOffset < -1 || transactionInfo.EventStreamId.IsEmptyString()) {
				Log.Error(
					"Invalid transaction info found for transaction ID {transactionId}. "
					+ "Possibly wrong transactionId provided. TransactionOffset: {transactionOffset}, EventStreamId: {stream}",
					transactionId,
					transactionInfo.TransactionOffset,
					transactionInfo.EventStreamId.IsEmptyString() ? "<null>" : transactionInfo.EventStreamId);
				return false;
			}

			return true;
		}

		void IHandle<StorageMessage.WriteCommit>.Handle(StorageMessage.WriteCommit message) {
			Interlocked.Decrement(ref FlushMessagesInQueue);
			try {
				var commitPos = Writer.Checkpoint.ReadNonFlushed();
				var commitCheck = _indexWriter.CheckCommitStartingAt(message.TransactionPosition, commitPos);
				if (commitCheck.Decision != CommitDecision.Ok) {
					ActOnCommitCheckFailure(message.Envelope, message.CorrelationId, commitCheck);
					return;
				}


				var commit = WriteCommitWithRetry(LogRecord.Commit(commitPos,
					message.CorrelationId,
					message.TransactionPosition,
					commitCheck.CurrentVersion + 1));

				bool softUndeleteMetastream = SystemStreams.IsMetastream(commitCheck.EventStreamId)
				                              &&
				                              _indexWriter.IsSoftDeleted(
					                              SystemStreams.OriginalStreamOf(commitCheck.EventStreamId));

				_indexWriter.PreCommit(commit);

				if (commitCheck.IsSoftDeleted)
					SoftUndeleteStream(commitCheck.EventStreamId, commitCheck.CurrentVersion + 1);
				if (softUndeleteMetastream)
					SoftUndeleteMetastream(commitCheck.EventStreamId);
			} catch (Exception exc) {
				Log.ErrorException(exc, "Exception in writer.");
				throw;
			} finally {
				Flush();
			}
		}

		private static void ActOnCommitCheckFailure(IEnvelope envelope, Guid correlationId, CommitCheckResult result) {
			switch (result.Decision) {
				case CommitDecision.WrongExpectedVersion:
					envelope.ReplyWith(new StorageMessage.WrongExpectedVersion(correlationId, result.CurrentVersion));
					break;
				case CommitDecision.Deleted:
					envelope.ReplyWith(new StorageMessage.StreamDeleted(correlationId));
					break;
				case CommitDecision.Idempotent:
					envelope.ReplyWith(new StorageMessage.AlreadyCommitted(correlationId,
						result.EventStreamId,
						result.StartEventNumber,
						result.EndEventNumber));
					break;
				case CommitDecision.CorruptedIdempotency:
					// in case of corrupted idempotency (part of transaction is ok, other is different)
					// then we can say that the transaction is not idempotent, so WrongExpectedVersion is ok answer
					envelope.ReplyWith(new StorageMessage.WrongExpectedVersion(correlationId, result.CurrentVersion));
					break;
				case CommitDecision.InvalidTransaction:
					envelope.ReplyWith(new StorageMessage.InvalidTransaction(correlationId));
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private WriteResult WritePrepareWithRetry(PrepareLogRecord prepare) {
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

		private CommitLogRecord WriteCommitWithRetry(CommitLogRecord commit) {
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

		protected bool Flush(bool force = false) {
			var start = _watch.ElapsedTicks;
			if (force || FlushMessagesInQueue == 0 || start - _lastFlushTimestamp >= _lastFlushDelay + _minFlushDelay) {
				var flushSize = Writer.Checkpoint.ReadNonFlushed() - Writer.Checkpoint.Read();

				Writer.Flush();
				HistogramService.SetValue(_writerFlushHistogram,
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

				return true;
			}

			return false;
		}

		private void PurgeNotProcessedInfo() {
			_indexWriter.PurgeNotProcessedCommitsTill(Db.Config.ChaserCheckpoint.Read());
			_indexWriter.PurgeNotProcessedTransactions(Db.Config.WriterCheckpoint.Read());
		}

		private struct WriteResult {
			public readonly long WrittenPos;
			public readonly long NewPos;
			public readonly PrepareLogRecord Prepare;

			public WriteResult(long writtenPos, long newPos, PrepareLogRecord prepare) {
				WrittenPos = writtenPos;
				NewPos = newPos;
				Prepare = prepare;
			}
		}

		public void Handle(MonitoringMessage.InternalStatsRequest message) {
			var lastFlushSize = Interlocked.Read(ref _lastFlushSize);
			var lastFlushDelayMs = Interlocked.Read(ref _lastFlushDelay) / (double)TicksPerMs;
			var statCount = _statCount;
			var meanFlushSize = statCount == 0 ? 0 : Interlocked.Read(ref _sumFlushSize) / statCount;
			var meanFlushDelayMs = statCount == 0
				? 0
				: Interlocked.Read(ref _sumFlushDelay) / (double)TicksPerMs / statCount;
			var maxFlushSize = Interlocked.Read(ref _maxFlushSize);
			var maxFlushDelayMs = Interlocked.Read(ref _maxFlushDelay) / (double)TicksPerMs;
			var queuedFlushMessages = FlushMessagesInQueue;

			var stats = new Dictionary<string, object> {
				{"es-writer-lastFlushSize", new StatMetadata(lastFlushSize, "Writer Last Flush Size")},
				{"es-writer-lastFlushDelayMs", new StatMetadata(lastFlushDelayMs, "Writer Last Flush Delay, ms")},
				{"es-writer-meanFlushSize", new StatMetadata(meanFlushSize, "Writer Mean Flush Size")},
				{"es-writer-meanFlushDelayMs", new StatMetadata(meanFlushDelayMs, "Writer Mean Flush Delay, ms")},
				{"es-writer-maxFlushSize", new StatMetadata(maxFlushSize, "Writer Max Flush Size")},
				{"es-writer-maxFlushDelayMs", new StatMetadata(maxFlushDelayMs, "Writer Max Flush Delay, ms")},
				{"es-writer-queuedFlushMessages", new StatMetadata(queuedFlushMessages, "Writer Queued Flush Message")}
			};

			message.Envelope.ReplyWith(new MonitoringMessage.InternalStatsRequestResponse(stats));
		}
	}
}
