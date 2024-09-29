// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DotNext.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.LogAbstraction;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Metrics;
using EventStore.Core.Services.Monitoring.Stats;
using EventStore.Core.Services.Storage.EpochManager;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Time;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.LogRecords;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ILogger = Serilog.ILogger;

namespace EventStore.Core.Services.Storage {
	public abstract class StorageWriterService {
	}

	public class StorageWriterService<TStreamId> : IHandle<SystemMessage.SystemInit>,
		IHandle<SystemMessage.StateChangeMessage>,
		IAsyncHandle<SystemMessage.WriteEpoch>,
		IHandle<SystemMessage.WaitForChaserToCatchUp>,
		IAsyncHandle<StorageMessage.WritePrepares>,
		IAsyncHandle<StorageMessage.WriteDelete>,
		IAsyncHandle<StorageMessage.WriteTransactionStart>,
		IAsyncHandle<StorageMessage.WriteTransactionData>,
		IAsyncHandle<StorageMessage.WriteTransactionEnd>,
		IAsyncHandle<StorageMessage.WriteCommit>,
		IHandle<MonitoringMessage.InternalStatsRequest> {
		private static readonly ILogger Log = Serilog.Log.ForContext<StorageWriterService>();
		private static EqualityComparer<TStreamId> StreamIdComparer { get; } = EqualityComparer<TStreamId>.Default;

		protected static readonly int TicksPerMs = (int)(Stopwatch.Frequency / 1000);
		private static readonly TimeSpan WaitForChaserSingleIterationTimeout = TimeSpan.FromMilliseconds(200);

		protected readonly TFChunkDb Db;
		protected readonly TFChunkWriter Writer;
		private readonly IIndexWriter<TStreamId> _indexWriter;
		private readonly IRecordFactory<TStreamId> _recordFactory;
		private readonly INameIndex<TStreamId> _streamNameIndex;
		private readonly INameIndex<TStreamId> _eventTypeIndex;
		private readonly ISystemStreamLookup<TStreamId> _systemStreams;
		private readonly IMaxTracker<long> _flushSizeTracker;
		private readonly IDurationMaxTracker _flushDurationTracker;

		protected readonly IEpochManager EpochManager;
		protected readonly IPublisher Bus;
		private readonly ISubscriber _subscribeToBus;
		protected readonly IQueuedHandler StorageWriterQueue;
		private readonly InMemoryBus _writerBus;

		private readonly Clock _clock = Clock.Instance;
		private readonly double _minFlushDelay;
		private long _lastFlushDelay;
		private Instant _lastFlushTimestamp;

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
		private readonly List<Task> _tasks = new List<Task>();
		private readonly TStreamId _emptyEventTypeId;
		private readonly TStreamId _scavengePointsStreamId;
		private readonly TStreamId _scavengePointEventTypeId;
		public IEnumerable<Task> Tasks {
			get { return _tasks; }
		}

		public StorageWriterService(IPublisher bus,
			ISubscriber subscribeToBus,
			TimeSpan minFlushDelay,
			TFChunkDb db,
			TFChunkWriter writer,
			IIndexWriter<TStreamId> indexWriter,
			IRecordFactory<TStreamId> recordFactory,
			INameIndex<TStreamId> streamNameIndex,
			INameIndex<TStreamId> eventTypeIndex,
			TStreamId emptyEventTypeId,
			ISystemStreamLookup<TStreamId> systemStreams,
			IEpochManager epochManager,
			QueueStatsManager queueStatsManager,
			QueueTrackers queueTrackers,
			IMaxTracker<long> flushSizeTracker,
			IDurationMaxTracker flushDurationTracker) {

			Ensure.NotNull(bus, "bus");
			Ensure.NotNull(subscribeToBus, "subscribeToBus");
			Ensure.NotNull(db, "db");
			Ensure.NotNull(writer, "writer");
			Ensure.NotNull(indexWriter, "indexWriter");
			Ensure.NotNull(recordFactory, nameof(recordFactory));
			Ensure.NotNull(streamNameIndex, nameof(streamNameIndex));
			Ensure.NotNull(eventTypeIndex, nameof(eventTypeIndex));
			Ensure.NotNull(systemStreams, nameof(systemStreams));
			Ensure.NotNull(epochManager, "epochManager");

			Bus = bus;
			_subscribeToBus = subscribeToBus;
			Db = db;
			_indexWriter = indexWriter;
			_recordFactory = recordFactory;
			_streamNameIndex = streamNameIndex;
			_eventTypeIndex = eventTypeIndex;
			_systemStreams = systemStreams;
			_emptyEventTypeId = emptyEventTypeId;
			EpochManager = epochManager;
			_flushDurationTracker = flushDurationTracker;
			_flushSizeTracker = flushSizeTracker;
			_scavengePointsStreamId = _streamNameIndex.GetExisting(SystemStreams.ScavengePointsStream);
			_scavengePointEventTypeId = _eventTypeIndex.GetExisting(SystemEventTypes.ScavengePoint);

			_minFlushDelay = minFlushDelay.TotalMilliseconds * TicksPerMs;
			_lastFlushDelay = 0;
			_lastFlushTimestamp = _clock.Now;

			Writer = writer;

			_writerBus = new InMemoryBus("StorageWriterBus", watchSlowMsg: false);
			StorageWriterQueue = new QueuedHandlerThreadPool(new AdHocHandler<Message>(CommonHandle),
				"StorageWriterQueue",
				queueStatsManager,
				queueTrackers,
				true,
				TimeSpan.FromMilliseconds(500));

			SubscribeToMessage<SystemMessage.SystemInit>();
			SubscribeToMessage<SystemMessage.StateChangeMessage>();
			SubscribeToMessage<SystemMessage.WriteEpoch>();
			SubscribeToMessage<SystemMessage.WaitForChaserToCatchUp>();
			SubscribeToMessage<StorageMessage.WritePrepares>();
			SubscribeToMessage<StorageMessage.WriteDelete>();
			SubscribeToMessage<StorageMessage.WriteTransactionStart>();
			SubscribeToMessage<StorageMessage.WriteTransactionData>();
			SubscribeToMessage<StorageMessage.WriteTransactionEnd>();
			SubscribeToMessage<StorageMessage.WriteCommit>();
		}

		public void Start() {
			Writer.Open();
			_tasks.Add(StorageWriterQueue.Start());
		}

		protected void SubscribeToMessage<T>() where T : Message {
			_writerBus.Subscribe((IAsyncHandle<T>)this);
			_subscribeToBus.Subscribe<T>(new AdHocHandler<Message>(EnqueueMessage));
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

		private async ValueTask CommonHandle(Message message, CancellationToken token) {
			if (BlockWriter && !(message is SystemMessage.StateChangeMessage)) {
				Log.Verbose("Blocking message {message} in StorageWriterService. Message:", message.GetType().Name);
				Log.Verbose("{message}", message);
				return;
			}

			if (_vnodeState != VNodeState.Leader && _vnodeState != VNodeState.ResigningLeader && message is StorageMessage.ILeaderWriteMessage) {
				Log.Fatal("{message} appeared in StorageWriter during state {vnodeStrate}.", message.GetType().Name,
					_vnodeState);
				var msg = String.Format("{0} appeared in StorageWriter during state {1}.", message.GetType().Name,
					_vnodeState);
				Application.Exit(ExitCode.Error, msg);
				return;
			}

			try {
				await _writerBus.DispatchAsync(message, token);
			} catch (Exception exc) {
				BlockWriter = true;
				Log.Fatal(exc, "Unexpected error in StorageWriterService. Terminating the process...");
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
				case VNodeState.Leader: {
						_indexWriter.Reset();
						_streamNameIndex.CancelReservations();
						_eventTypeIndex.CancelReservations();
						break;
					}
				case VNodeState.ShuttingDown: {
						Writer.Close();
						break;
					}
			}
		}

		async ValueTask IAsyncHandle<SystemMessage.WriteEpoch>.HandleAsync(SystemMessage.WriteEpoch message, CancellationToken token) {
			if (_vnodeState != VNodeState.Leader && _vnodeState != VNodeState.PreLeader)
				throw new Exception(string.Format("New Epoch request not in leader or preleader state. State: {0}.", _vnodeState));

			if (Writer.NeedsNewChunk)
				await Writer.AddNewChunk(token: token);

			await EpochManager.WriteNewEpoch(message.EpochNumber, token);
			PurgeNotProcessedInfo();
		}

		void IHandle<SystemMessage.WaitForChaserToCatchUp>.Handle(SystemMessage.WaitForChaserToCatchUp message) {
			// if we are in states, that doesn't need to wait for chaser, ignore
			if (_vnodeState != VNodeState.PreLeader &&
				_vnodeState != VNodeState.PreReplica &&
				_vnodeState != VNodeState.PreReadOnlyReplica)
				throw new Exception(string.Format("{0} appeared in {1} state.", message.GetType().Name, _vnodeState));

			if (Writer.HasOpenTransaction())
				throw new InvalidOperationException("Writer has an open transaction.");

			if (Writer.FlushedPosition != Writer.Position) {
				Writer.Flush();
				Bus.Publish(new ReplicationTrackingMessage.WriterCheckpointFlushed());
			}

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

		async ValueTask IAsyncHandle<StorageMessage.WritePrepares>.HandleAsync(StorageMessage.WritePrepares msg, CancellationToken token) {
			Interlocked.Decrement(ref FlushMessagesInQueue);

			var cts = token.LinkTo(msg.CancellationToken);
			try {
				if (token.IsCancellationRequested)
					return;

				var logPosition = Writer.Position;
				var prepares = new List<IPrepareLogRecord<TStreamId>>();

				var preExisting = _streamNameIndex.GetOrReserve(
					recordFactory: _recordFactory,
					streamName: msg.EventStreamId,
					logPosition: logPosition,
					streamId: out var streamId,
					streamRecord: out var streamRecord);

				if (streamRecord != null) {
					prepares.Add(streamRecord);
					logPosition += streamRecord.SizeOnDisk;
				}

				var commitCheck = _indexWriter.CheckCommit(streamId, msg.ExpectedVersion,
					msg.Events.Select(x => x.EventId), streamMightExist: preExisting);
				if (commitCheck.Decision != CommitDecision.Ok) {
					ActOnCommitCheckFailure(msg.Envelope, msg.CorrelationId, commitCheck);
					return;
				}

				if (msg.Events.Length > 0) {
					var eventTypes = new TStreamId[msg.Events.Length]; // todo: pool
					for (int i = 0; i < msg.Events.Length; ++i) {
						var evnt = msg.Events[i];
						GetOrReserveEventType(evnt.EventType, logPosition, out eventTypes[i], out var eventTypeRecord);
						if (eventTypeRecord != null) {
							prepares.Add(eventTypeRecord);
							logPosition += eventTypeRecord.SizeOnDisk;
						}
					}

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
						var prepare = LogRecord.Prepare(_recordFactory, logPosition, msg.CorrelationId, evnt.EventId,
							transactionPosition, i, streamId,
							expectedVersion, flags, eventTypes[i], evnt.Data, evnt.Metadata);
						prepares.Add(prepare);

						logPosition += prepare.SizeOnDisk;
					}
				} else {
					prepares.Add(
						LogRecord.Prepare(_recordFactory, logPosition, msg.CorrelationId, Guid.NewGuid(), logPosition, -1,
							streamId, commitCheck.CurrentVersion,
							PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd | PrepareFlags.IsCommitted,
							_emptyEventTypeId, Empty.ByteArray, Empty.ByteArray));
				}

				if (!await TryWritePreparesWithRetry(prepares, token)) {
					ActOnCommitCheckFailure(
						envelope: msg.Envelope,
						correlationId: msg.CorrelationId,
						result: new CommitCheckResult<TStreamId>(
							decision: CommitDecision.InvalidTransaction,
							eventStreamId: streamId,
							currentVersion: commitCheck.CurrentVersion,
							startEventNumber: -1,
							endEventNumber: -1,
							isSoftDeleted: false));
				}

				bool softUndeleteMetastream = _systemStreams.IsMetaStream(streamId)
											  && _indexWriter.IsSoftDeleted(_systemStreams.OriginalStreamOf(streamId));

				// note: the stream & event type records are indexed separately and must not be pre-committed to the main index
				_indexWriter.PreCommit(CollectionsMarshal.AsSpan(prepares)[^msg.Events.Length..]);

				if (commitCheck.IsSoftDeleted)
					await SoftUndeleteStream(streamId, commitCheck.CurrentVersion + 1, token);
				if (softUndeleteMetastream)
					await SoftUndeleteMetastream(streamId, token);
			} catch (Exception exc) {
				Log.Error(exc, "Exception in writer.");
				throw;
			} finally {
				Flush();
				cts?.Dispose();
			}
		}

		private bool GetOrReserveEventType(string eventType, long logPosition,
			out TStreamId eventTypeId, out IPrepareLogRecord<TStreamId> eventTypeRecord) {
			return _eventTypeIndex.GetOrReserveEventType(
				recordFactory: _recordFactory,
				eventType: eventType,
				logPosition: logPosition,
				eventTypeId: out eventTypeId,
				eventTypeRecord: out eventTypeRecord);
		}

		private async ValueTask<(TStreamId, long)> GetOrWriteEventType(string eventType, long logPosition, CancellationToken token) {
			GetOrReserveEventType(eventType, logPosition, out var eventTypeId, out var eventTypeRecord);

			if (eventTypeRecord is not null)
			{
				var result = await WritePrepareWithRetry(eventTypeRecord, token);
				logPosition = result.NewPos;
			}

			return (eventTypeId, logPosition);
		}

		private ValueTask SoftUndeleteMetastream(TStreamId metastreamId, CancellationToken token) {
			var origStreamId = _systemStreams.OriginalStreamOf(metastreamId);
			var rawMetaInfo = _indexWriter.GetStreamRawMeta(origStreamId);
			return SoftUndeleteStream(origStreamId, rawMetaInfo.MetaLastEventNumber, rawMetaInfo.RawMeta,
				recreateFrom: _indexWriter.GetStreamLastEventNumber(origStreamId) + 1, token);
		}

		private ValueTask SoftUndeleteStream(TStreamId streamId, long recreateFromEventNumber, CancellationToken token) {
			var rawInfo = _indexWriter.GetStreamRawMeta(streamId);
			return SoftUndeleteStream(streamId, rawInfo.MetaLastEventNumber, rawInfo.RawMeta, recreateFromEventNumber, token);
		}

		private async ValueTask SoftUndeleteStream(TStreamId streamId, long metaLastEventNumber, ReadOnlyMemory<byte> rawMeta, long recreateFrom, CancellationToken token) {
			byte[] modifiedMeta;
			if (!SoftUndeleteRawMeta(rawMeta, recreateFrom, out modifiedMeta))
				return;

			var logPosition = Writer.Position;
			(var streamMetadataEventTypeId, logPosition) =
				await GetOrWriteEventType(SystemEventTypes.StreamMetadata, logPosition, token);

			var res = await WritePrepareWithRetry(
				LogRecord.Prepare(_recordFactory, logPosition, Guid.NewGuid(), Guid.NewGuid(), logPosition, 0,
					_systemStreams.MetaStreamOf(streamId), metaLastEventNumber,
					PrepareFlags.SingleWrite | PrepareFlags.IsCommitted | PrepareFlags.IsJson,
					streamMetadataEventTypeId, modifiedMeta, Empty.ByteArray),
				token);

			_indexWriter.PreCommit(new[] { res.Prepare });
		}

		public bool SoftUndeleteRawMeta(ReadOnlyMemory<byte> rawMeta, long recreateFromEventNumber, out byte[] modifiedMeta) {
			try {
				var jobj = JObject.Parse(Encoding.UTF8.GetString(rawMeta.Span));
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

		async ValueTask IAsyncHandle<StorageMessage.WriteDelete>.HandleAsync(StorageMessage.WriteDelete message, CancellationToken token) {
			Interlocked.Decrement(ref FlushMessagesInQueue);
			try {
				if (message.CancellationToken.IsCancellationRequested)
					return;

				var eventId = Guid.NewGuid();

				var logPosition = Writer.Position;

				var preExisting = _streamNameIndex.GetOrReserve(
					recordFactory: _recordFactory,
					streamName: message.EventStreamId,
					logPosition: logPosition,
					streamId: out var streamId,
					streamRecord: out var streamRecord);

				if (streamRecord != null) {
					var res = await WritePrepareWithRetry(streamRecord, token);
					logPosition = res.NewPos;
				}

				var commitCheck = _indexWriter.CheckCommit(streamId, message.ExpectedVersion,
					[eventId], streamMightExist: preExisting);
				if (commitCheck.Decision != CommitDecision.Ok) {
					ActOnCommitCheckFailure(message.Envelope, message.CorrelationId, commitCheck);
					return;
				}

				if (message.HardDelete) {
					// HARD DELETE
					const long expectedVersion = EventNumber.DeletedStream - 1;
					(var streamDeletedEventType, logPosition) =
						await GetOrWriteEventType(SystemEventTypes.StreamDeleted, logPosition, token);
					var record = LogRecord.DeleteTombstone(_recordFactory, logPosition, message.CorrelationId,
						eventId, streamId, streamDeletedEventType,
						expectedVersion, PrepareFlags.IsCommitted);
					var res = await WritePrepareWithRetry(record, token);
					_indexWriter.PreCommit([res.Prepare]);
				} else {
					// SOFT DELETE
					var metastreamId = _systemStreams.MetaStreamOf(streamId);
					var expectedVersion = _indexWriter.GetStreamLastEventNumber(metastreamId);

					if (_indexWriter.GetStreamLastEventNumber(streamId) < 0 && expectedVersion < 0) {
						var result = new CommitCheckResult<TStreamId>(CommitDecision.WrongExpectedVersion, streamId,
							-1, -1, -1, false);
						ActOnCommitCheckFailure(message.Envelope, message.CorrelationId, result);
						return;
					}


					const PrepareFlags flags = PrepareFlags.SingleWrite | PrepareFlags.IsCommitted |
											   PrepareFlags.IsJson;
					var data = new StreamMetadata(truncateBefore: EventNumber.DeletedStream).ToJsonBytes();

					(var streamMetadataEventTypeId, logPosition) =
						await GetOrWriteEventType(SystemEventTypes.StreamMetadata, logPosition, token);

					var res = await WritePrepareWithRetry(
						LogRecord.Prepare(_recordFactory, logPosition, message.CorrelationId, eventId, logPosition, 0,
							metastreamId, expectedVersion, flags, streamMetadataEventTypeId,
							data, null), token);
					_indexWriter.PreCommit([res.Prepare]);
				}
			} catch (Exception exc) {
				Log.Error(exc, "Exception in writer.");
				throw;
			} finally {
				Flush();
			}
		}

		async ValueTask IAsyncHandle<StorageMessage.WriteTransactionStart>.HandleAsync(StorageMessage.WriteTransactionStart message, CancellationToken token) {
			Interlocked.Decrement(ref FlushMessagesInQueue);
			try {
				if (message.LiveUntil < DateTime.UtcNow)
					return;

				var streamId = _indexWriter.GetStreamId(message.EventStreamId);
				var record = LogRecord.TransactionBegin(_recordFactory, Writer.Position,
					message.CorrelationId,
					streamId,
					message.ExpectedVersion);
				var res = await WritePrepareWithRetry(record, token);

				// we update cache to avoid non-cached look-up on next TransactionWrite
				_indexWriter.UpdateTransactionInfo(res.WrittenPos, res.WrittenPos,
					new TransactionInfo<TStreamId>(-1, streamId));
			} catch (Exception exc) {
				Log.Error(exc, "Exception in writer.");
				throw;
			} finally {
				Flush();
			}
		}

		async ValueTask IAsyncHandle<StorageMessage.WriteTransactionData>.HandleAsync(StorageMessage.WriteTransactionData message, CancellationToken token) {
			Interlocked.Decrement(ref FlushMessagesInQueue);
			try {
				var logPosition = Writer.Position;
				var transactionInfo = await _indexWriter.GetTransactionInfo(Writer.FlushedPosition, message.TransactionId, token);
				if (!CheckTransactionInfo(message.TransactionId, transactionInfo))
					return;

				if (message.Events.Length > 0) {
					long lastLogPosition = -1;
					for (int i = 0; i < message.Events.Length; ++i) {
						var evnt = message.Events[i];
						// safe, only v2 supports transactions and it doesnt write eventtype records.
						(var eventType, logPosition) = await GetOrWriteEventType(evnt.EventType, logPosition, token);
						var record = LogRecord.TransactionWrite(
							_recordFactory,
							logPosition,
							message.CorrelationId,
							evnt.EventId,
							message.TransactionId,
							transactionInfo.TransactionOffset + i + 1,
							transactionInfo.EventStreamId,
							eventType,
							evnt.Data,
							evnt.Metadata,
							evnt.IsJson);
						var res = await WritePrepareWithRetry(record, token);
						logPosition = res.NewPos;
						lastLogPosition = res.WrittenPos;
					}

					var info = transactionInfo with {
						TransactionOffset = transactionInfo.TransactionOffset + message.Events.Length
					};

					_indexWriter.UpdateTransactionInfo(message.TransactionId, lastLogPosition, info);
				}
			} catch (Exception exc) {
				Log.Error(exc, "Exception in writer.");
				throw;
			} finally {
				Flush();
			}
		}

		async ValueTask IAsyncHandle<StorageMessage.WriteTransactionEnd>.HandleAsync(StorageMessage.WriteTransactionEnd message, CancellationToken token) {
			Interlocked.Decrement(ref FlushMessagesInQueue);
			try {
				if (message.LiveUntil < DateTime.UtcNow)
					return;

				var transactionInfo = await _indexWriter.GetTransactionInfo(Writer.FlushedPosition, message.TransactionId, token);
				if (!CheckTransactionInfo(message.TransactionId, transactionInfo))
					return;

				var record = LogRecord.TransactionEnd(_recordFactory, Writer.Position,
					message.CorrelationId,
					Guid.NewGuid(),
					message.TransactionId,
					transactionInfo.EventStreamId);
				await WritePrepareWithRetry(record, token);
			} catch (Exception exc) {
				Log.Error(exc, "Exception in writer.");
				throw;
			} finally {
				Flush();
			}
		}

		private static bool CheckTransactionInfo(long transactionId, TransactionInfo<TStreamId> transactionInfo) {
			var noStreamId = StreamIdComparer.Equals(transactionInfo.EventStreamId, default);
			if (transactionInfo.TransactionOffset < -1 || noStreamId) {
				Log.Error(
					"Invalid transaction info found for transaction ID {transactionId}. "
					+ "Possibly wrong transactionId provided. TransactionOffset: {transactionOffset}, EventStreamId: {stream}",
					transactionId,
					transactionInfo.TransactionOffset,
					noStreamId ? "<null>" : $"{transactionInfo.EventStreamId}");
				return false;
			}

			return true;
		}

		async ValueTask IAsyncHandle<StorageMessage.WriteCommit>.HandleAsync(StorageMessage.WriteCommit message, CancellationToken token) {
			Interlocked.Decrement(ref FlushMessagesInQueue);
			try {
				var commitPos = Writer.Position;
				var commitCheck = _indexWriter.CheckCommitStartingAt(message.TransactionPosition, commitPos);
				if (commitCheck.Decision != CommitDecision.Ok) {
					ActOnCommitCheckFailure(message.Envelope, message.CorrelationId, commitCheck);
					return;
				}


				var commit = await WriteCommitWithRetry(LogRecord.Commit(commitPos,
					message.CorrelationId,
					message.TransactionPosition,
					commitCheck.CurrentVersion + 1),
					token);

				bool softUndeleteMetastream = _systemStreams.IsMetaStream(commitCheck.EventStreamId)
											  &&
											  _indexWriter.IsSoftDeleted(
												  _systemStreams.OriginalStreamOf(commitCheck.EventStreamId));

				_indexWriter.PreCommit(commit);

				if (commitCheck.IsSoftDeleted)
					await SoftUndeleteStream(commitCheck.EventStreamId, commitCheck.CurrentVersion + 1, token);
				if (softUndeleteMetastream)
					await SoftUndeleteMetastream(commitCheck.EventStreamId, token);
			} catch (Exception exc) {
				Log.Error(exc, "Exception in writer.");
				throw;
			} finally {
				Flush();
			}
		}

		private void ActOnCommitCheckFailure(IEnvelope envelope, Guid correlationId, CommitCheckResult<TStreamId> result) {
			switch (result.Decision) {
				case CommitDecision.WrongExpectedVersion:
					envelope.ReplyWith(new StorageMessage.WrongExpectedVersion(correlationId, result.CurrentVersion));
					break;
				case CommitDecision.Deleted:
					envelope.ReplyWith(new StorageMessage.StreamDeleted(correlationId));
					break;
				case CommitDecision.Idempotent:
					var eventStreamName = _indexWriter.GetStreamName(result.EventStreamId);
					envelope.ReplyWith(new StorageMessage.AlreadyCommitted(correlationId,
						eventStreamName,
						result.StartEventNumber,
						result.EndEventNumber,
						result.IdempotentLogPosition));
					break;
				case CommitDecision.CorruptedIdempotency:
					// in case of corrupted idempotency (part of transaction is ok, other is different)
					// then we can say that the transaction is not idempotent, so WrongExpectedVersion is ok answer
					envelope.ReplyWith(new StorageMessage.WrongExpectedVersion(correlationId, result.CurrentVersion));
					break;
				case CommitDecision.InvalidTransaction:
					envelope.ReplyWith(new StorageMessage.InvalidTransaction(correlationId));
					break;
				case CommitDecision.IdempotentNotReady:
					//TODO(clc): when we have the pre-index we should be able to get the logPosition from the pre-index and allow the transaction to wait for the cluster commit
					//just drop the write and wait for the client to retry
					Log.Debug("Dropping idempotent write to stream {@stream}, startEventNumber: {@startEventNumber}, endEventNumber: {@endEventNumber} since the original write has not yet been replicated.", result.EventStreamId, result.StartEventNumber, result.EndEventNumber);
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private async ValueTask<bool> TryWritePreparesWithRetry(IList<IPrepareLogRecord<TStreamId>> prepares, CancellationToken token) {
			Ensure.Positive(prepares.Count, nameof(prepares.Count));

			if (prepares.Count is 1) {
				await WritePrepareWithRetry(prepares[0], token);
				return true;
			}

			var prepareSizes = 0;
			foreach (var prepare in prepares)
				prepareSizes += prepare.SizeOnDisk;

			if (prepareSizes > Db.Config.ChunkSize) {
				Log.Error("Transaction size ({prepareSizes:N0}) exceeds chunk size ({chunkSize:N0})",
					prepareSizes, Db.Config.ChunkSize);
				return false;
			}

			if (!Writer.CanWrite(prepareSizes)) {
				Writer.CompleteChunk();
				await Writer.AddNewChunk(token: token);
				if (!Writer.CanWrite(prepareSizes)) {
					throw new Exception($"Transaction of size {prepareSizes:N0} cannot be written even after completing a chunk");
				}

				long logPos = Writer.Position;
				long transactionPos = default;

				for (int i = 0; i < prepares.Count; i++) {
					// the prepares may be from different streams due to the stream & event type records
					// we thus adjust the transaction position to the correct value on each stream id change
					if (i is 0 || !StreamIdComparer.Equals(prepares[i].EventStreamId, prepares[i - 1].EventStreamId))
						transactionPos = logPos;

					prepares[i] = prepares[i].CopyForRetry(logPos, transactionPos);
					logPos += prepares[i].SizeOnDisk;
				}
			}

			Writer.OpenTransaction();
			var writerPos = Writer.Position;
			foreach (var prepare in prepares)
			{
				Writer.WriteToTransaction(prepare, out var newWriterPos);
				if (newWriterPos - writerPos != prepare.SizeOnDisk)
					throw new Exception($"Expected writer position to be at: {writerPos + prepare.SizeOnDisk} but it was at {newWriterPos}");

				writerPos = newWriterPos;
			}
			Writer.CommitTransaction();

			return true;
		}

		private async ValueTask<WriteResult> WritePrepareWithRetry(IPrepareLogRecord<TStreamId> prepare, CancellationToken token) {
			long writtenPos = prepare.LogPosition;
			var record = prepare;

			var (written, newPos) = await Writer.Write(prepare, token);
			if (!written) {
				var transactionPos = prepare.TransactionPosition == prepare.LogPosition
					? newPos
					: prepare.TransactionPosition;

				record = prepare.CopyForRetry(
					logPosition: newPos,
					transactionPosition: transactionPos);

				writtenPos = newPos;
				(written, newPos) = await Writer.Write(record, token);
				if (!written) {
					throw new Exception(
						string.Format("Second write try failed when first writing prepare at {0}, then at {1}.",
							prepare.LogPosition,
							writtenPos));
				}
			}

			if (StreamIdComparer.Equals(prepare.EventType, _scavengePointEventTypeId) &&
				StreamIdComparer.Equals(prepare.EventStreamId, _scavengePointsStreamId)) {
				Writer.CompleteChunk();
				await Writer.AddNewChunk(token: token);
			}

			return new WriteResult(writtenPos, newPos, record);
		}

		private async ValueTask<CommitLogRecord> WriteCommitWithRetry(CommitLogRecord commit, CancellationToken token) {
			if (await Writer.Write(commit, token) is (false, var newPos)) {
				var transactionPos = commit.TransactionPosition == commit.LogPosition
					? newPos
					: commit.TransactionPosition;
				var record = new CommitLogRecord(newPos,
					commit.CorrelationId,
					transactionPos,
					commit.TimeStamp,
					commit.FirstEventNumber);
				long writtenPos = newPos;
				if (await Writer.Write(record, token) is (false, _)) {
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
			var start = _clock.Now;
			if (force || FlushMessagesInQueue == 0 || start.ElapsedTicksSince(_lastFlushTimestamp) >= _lastFlushDelay + _minFlushDelay) {
				var flushSize = Writer.Position - Writer.FlushedPosition;

				Writer.Flush();

				_flushSizeTracker.Record(flushSize);
				var end = _flushDurationTracker.RecordNow(start);

				var flushDelay = end.ElapsedTicksSince(start);

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
				Bus.Publish(new ReplicationTrackingMessage.WriterCheckpointFlushed());
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
			public readonly IPrepareLogRecord<TStreamId> Prepare;

			public WriteResult(long writtenPos, long newPos, IPrepareLogRecord<TStreamId> prepare) {
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
