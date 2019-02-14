using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.DataStructures;
using EventStore.Core.Settings;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Services.Storage.ReaderIndex {
	public interface IIndexWriter {
		long CachedTransInfo { get; }
		long NotCachedTransInfo { get; }

		void Reset();
		CommitCheckResult CheckCommitStartingAt(long transactionPosition, long commitPosition);
		CommitCheckResult CheckCommit(string streamId, long expectedVersion, IEnumerable<Guid> eventIds);
		void PreCommit(CommitLogRecord commit);
		void PreCommit(IList<PrepareLogRecord> commitedPrepares);
		void UpdateTransactionInfo(long transactionId, long logPosition, TransactionInfo transactionInfo);
		TransactionInfo GetTransactionInfo(long writerCheckpoint, long transactionId);
		void PurgeNotProcessedCommitsTill(long checkpoint);
		void PurgeNotProcessedTransactions(long checkpoint);

		bool IsSoftDeleted(string streamId);
		long GetStreamLastEventNumber(string streamId);
		StreamMetadata GetStreamMetadata(string streamId);
		RawMetaInfo GetStreamRawMeta(string streamId);
	}

	public struct RawMetaInfo {
		public readonly long MetaLastEventNumber;
		public readonly byte[] RawMeta;

		public RawMetaInfo(long metaLastEventNumber, byte[] rawMeta) {
			MetaLastEventNumber = metaLastEventNumber;
			RawMeta = rawMeta;
		}
	}

	public class IndexWriter : IIndexWriter {
		private static readonly ILogger Log = LogManager.GetLoggerFor<IndexWriter>();

		public long CachedTransInfo {
			get { return Interlocked.Read(ref _cachedTransInfo); }
		}

		public long NotCachedTransInfo {
			get { return Interlocked.Read(ref _notCachedTransInfo); }
		}

		private readonly IIndexBackend _indexBackend;
		private readonly IIndexReader _indexReader;

		private readonly IStickyLRUCache<long, TransactionInfo> _transactionInfoCache =
			new StickyLRUCache<long, TransactionInfo>(ESConsts.TransactionMetadataCacheCapacity);

		private readonly Queue<TransInfo> _notProcessedTrans = new Queue<TransInfo>();

		private readonly BoundedCache<Guid, EventInfo> _committedEvents =
			new BoundedCache<Guid, EventInfo>(int.MaxValue, ESConsts.CommitedEventsMemCacheLimit,
				x => 16 + 4 + IntPtr.Size + 2 * x.StreamId.Length);

		private readonly IStickyLRUCache<string, long> _streamVersions =
			new StickyLRUCache<string, long>(ESConsts.StreamInfoCacheCapacity);

		private readonly IStickyLRUCache<string, StreamMeta>
			_streamRawMetas =
				new StickyLRUCache<string, StreamMeta>(0); // store nothing flushed, only sticky non-flushed stuff

		private readonly Queue<CommitInfo> _notProcessedCommits = new Queue<CommitInfo>();

		private long _cachedTransInfo;
		private long _notCachedTransInfo;

		public IndexWriter(IIndexBackend indexBackend, IIndexReader indexReader) {
			Ensure.NotNull(indexBackend, "indexBackend");
			Ensure.NotNull(indexReader, "indexReader");

			_indexBackend = indexBackend;
			_indexReader = indexReader;
		}

		public void Reset() {
			_notProcessedCommits.Clear();
			_streamVersions.Clear();
			_streamRawMetas.Clear();
			_notProcessedTrans.Clear();
			_transactionInfoCache.Clear();
		}

		public CommitCheckResult CheckCommitStartingAt(long transactionPosition, long commitPosition) {
			string streamId;
			long expectedVersion;
			using (var reader = _indexBackend.BorrowReader()) {
				try {
					PrepareLogRecord prepare = GetPrepare(reader, transactionPosition);
					if (prepare == null) {
						Log.Error("Could not read first prepare of to-be-committed transaction. "
						          + "Transaction pos: {transactionPosition}, commit pos: {commitPosition}.",
							transactionPosition, commitPosition);
						var message = String.Format("Could not read first prepare of to-be-committed transaction. "
						                            + "Transaction pos: {0}, commit pos: {1}.",
							transactionPosition, commitPosition);
						throw new InvalidOperationException(message);
					}

					streamId = prepare.EventStreamId;
					expectedVersion = prepare.ExpectedVersion;
				} catch (InvalidOperationException) {
					return new CommitCheckResult(CommitDecision.InvalidTransaction, string.Empty, -1, -1, -1, false);
				}
			}

			// we should skip prepares without data, as they don't mean anything for idempotency
			// though we have to check deletes, otherwise they always will be considered idempotent :)
			var eventIds = from prepare in GetTransactionPrepares(transactionPosition, commitPosition)
				where prepare.Flags.HasAnyOf(PrepareFlags.Data | PrepareFlags.StreamDelete)
				select prepare.EventId;
			return CheckCommit(streamId, expectedVersion, eventIds);
		}

		private static PrepareLogRecord GetPrepare(TFReaderLease reader, long logPosition) {
			RecordReadResult result = reader.TryReadAt(logPosition);
			if (!result.Success)
				return null;
			if (result.LogRecord.RecordType != LogRecordType.Prepare)
				throw new Exception(string.Format("Incorrect type of log record {0}, expected Prepare record.",
					result.LogRecord.RecordType));
			return (PrepareLogRecord)result.LogRecord;
		}

		public CommitCheckResult CheckCommit(string streamId, long expectedVersion, IEnumerable<Guid> eventIds) {
			var curVersion = GetStreamLastEventNumber(streamId);
			if (curVersion == EventNumber.DeletedStream)
				return new CommitCheckResult(CommitDecision.Deleted, streamId, curVersion, -1, -1, false);
			if (curVersion == EventNumber.Invalid)
				return new CommitCheckResult(CommitDecision.WrongExpectedVersion, streamId, curVersion, -1, -1, false);

			if (expectedVersion == ExpectedVersion.StreamExists) {
				if (IsSoftDeleted(streamId))
					return new CommitCheckResult(CommitDecision.Deleted, streamId, curVersion, -1, -1, true);

				if (curVersion < 0) {
					var metadataVersion = GetStreamLastEventNumber(SystemStreams.MetastreamOf(streamId));
					if (metadataVersion < 0)
						return new CommitCheckResult(CommitDecision.WrongExpectedVersion, streamId, curVersion, -1, -1,
							false);
				}
			}

			// idempotency checks
			if (expectedVersion == ExpectedVersion.Any || expectedVersion == ExpectedVersion.StreamExists) {
				var first = true;
				long startEventNumber = -1;
				long endEventNumber = -1;
				foreach (var eventId in eventIds) {
					EventInfo prepInfo;
					if (!_committedEvents.TryGetRecord(eventId, out prepInfo) || prepInfo.StreamId != streamId)
						return new CommitCheckResult(first ? CommitDecision.Ok : CommitDecision.CorruptedIdempotency,
							streamId, curVersion, -1, -1, first && IsSoftDeleted(streamId));
					if (first)
						startEventNumber = prepInfo.EventNumber;
					endEventNumber = prepInfo.EventNumber;
					first = false;
				}

				return first /* no data in transaction */
					? new CommitCheckResult(CommitDecision.Ok, streamId, curVersion, -1, -1, IsSoftDeleted(streamId))
					: new CommitCheckResult(CommitDecision.Idempotent, streamId, curVersion, startEventNumber,
						endEventNumber, false);
			}

			if (expectedVersion < curVersion) {
				var eventNumber = expectedVersion;
				foreach (var eventId in eventIds) {
					eventNumber += 1;

					EventInfo prepInfo;
					if (_committedEvents.TryGetRecord(eventId, out prepInfo)
					    && prepInfo.StreamId == streamId
					    && prepInfo.EventNumber == eventNumber)
						continue;

					var res = _indexReader.ReadPrepare(streamId, eventNumber);
					if (res != null && res.EventId == eventId)
						continue;

					var first = eventNumber == expectedVersion + 1;
					if (!first)
						return new CommitCheckResult(CommitDecision.CorruptedIdempotency, streamId, curVersion, -1, -1,
							false);

					if (expectedVersion == ExpectedVersion.NoStream && IsSoftDeleted(streamId))
						return new CommitCheckResult(CommitDecision.Ok, streamId, curVersion, -1, -1, true);

					return new CommitCheckResult(CommitDecision.WrongExpectedVersion, streamId, curVersion, -1, -1,
						false);
				}

				return eventNumber == expectedVersion /* no data in transaction */
					? new CommitCheckResult(CommitDecision.WrongExpectedVersion, streamId, curVersion, -1, -1, false)
					: new CommitCheckResult(CommitDecision.Idempotent, streamId, curVersion, expectedVersion + 1,
						eventNumber, false);
			}

			if (expectedVersion > curVersion)
				return new CommitCheckResult(CommitDecision.WrongExpectedVersion, streamId, curVersion, -1, -1, false);

			// expectedVersion == currentVersion
			return new CommitCheckResult(CommitDecision.Ok, streamId, curVersion, -1, -1, IsSoftDeleted(streamId));
		}

		public void PreCommit(CommitLogRecord commit) {
			string streamId = null;
			long eventNumber = EventNumber.Invalid;
			PrepareLogRecord lastPrepare = null;

			foreach (var prepare in GetTransactionPrepares(commit.TransactionPosition, commit.LogPosition)) {
				if (prepare.Flags.HasNoneOf(PrepareFlags.StreamDelete | PrepareFlags.Data))
					continue;

				if (streamId == null)
					streamId = prepare.EventStreamId;

				if (prepare.EventStreamId != streamId)
					throw new Exception(string.Format("Expected stream: {0}, actual: {1}.", streamId,
						prepare.EventStreamId));

				eventNumber = prepare.Flags.HasAnyOf(PrepareFlags.StreamDelete)
					? EventNumber.DeletedStream
					: commit.FirstEventNumber + prepare.TransactionOffset;
				lastPrepare = prepare;
				_committedEvents.PutRecord(prepare.EventId, new EventInfo(streamId, eventNumber),
					throwOnDuplicate: false);
			}

			if (eventNumber != EventNumber.Invalid)
				_streamVersions.Put(streamId, eventNumber, +1);

			if (lastPrepare != null && SystemStreams.IsMetastream(streamId)) {
				var rawMeta = lastPrepare.Data;
				_streamRawMetas.Put(SystemStreams.OriginalStreamOf(streamId), new StreamMeta(rawMeta, null), +1);
			}
		}

		public void PreCommit(IList<PrepareLogRecord> commitedPrepares) {
			if (commitedPrepares.Count == 0)
				return;

			var lastPrepare = commitedPrepares[commitedPrepares.Count - 1];
			string streamId = lastPrepare.EventStreamId;
			long eventNumber = EventNumber.Invalid;
			foreach (var prepare in commitedPrepares) {
				if (prepare.Flags.HasNoneOf(PrepareFlags.StreamDelete | PrepareFlags.Data))
					continue;

				if (prepare.EventStreamId != streamId)
					throw new Exception(string.Format("Expected stream: {0}, actual: {1}.", streamId,
						prepare.EventStreamId));

				eventNumber =
					prepare.ExpectedVersion + 1; /* for committed prepare expected version is always explicit */
				_committedEvents.PutRecord(prepare.EventId, new EventInfo(streamId, eventNumber),
					throwOnDuplicate: false);
			}

			_notProcessedCommits.Enqueue(new CommitInfo(streamId, lastPrepare.LogPosition));
			_streamVersions.Put(streamId, eventNumber, 1);
			if (SystemStreams.IsMetastream(streamId)) {
				var rawMeta = lastPrepare.Data;
				_streamRawMetas.Put(SystemStreams.OriginalStreamOf(streamId), new StreamMeta(rawMeta, null), +1);
			}
		}

		public void UpdateTransactionInfo(long transactionId, long logPosition, TransactionInfo transactionInfo) {
			_notProcessedTrans.Enqueue(new TransInfo(transactionId, logPosition));
			_transactionInfoCache.Put(transactionId, transactionInfo, +1);
		}

		public TransactionInfo GetTransactionInfo(long writerCheckpoint, long transactionId) {
			TransactionInfo transactionInfo;
			if (!_transactionInfoCache.TryGet(transactionId, out transactionInfo)) {
				if (GetTransactionInfoUncached(writerCheckpoint, transactionId, out transactionInfo))
					_transactionInfoCache.Put(transactionId, transactionInfo, 0);
				else
					transactionInfo = new TransactionInfo(int.MinValue, null);
				Interlocked.Increment(ref _notCachedTransInfo);
			} else {
				Interlocked.Increment(ref _cachedTransInfo);
			}

			return transactionInfo;
		}

		private bool GetTransactionInfoUncached(long writerCheckpoint, long transactionId,
			out TransactionInfo transactionInfo) {
			using (var reader = _indexBackend.BorrowReader()) {
				reader.Reposition(writerCheckpoint);
				SeqReadResult result;
				while ((result = reader.TryReadPrev()).Success) {
					if (result.LogRecord.LogPosition < transactionId)
						break;
					if (result.LogRecord.RecordType != LogRecordType.Prepare)
						continue;
					var prepare = (PrepareLogRecord)result.LogRecord;
					if (prepare.TransactionPosition == transactionId) {
						transactionInfo = new TransactionInfo(prepare.TransactionOffset, prepare.EventStreamId);
						return true;
					}
				}
			}

			transactionInfo = new TransactionInfo(int.MinValue, null);
			return false;
		}

		public void PurgeNotProcessedCommitsTill(long checkpoint) {
			while (_notProcessedCommits.Count > 0 && _notProcessedCommits.Peek().LogPosition < checkpoint) {
				var commitInfo = _notProcessedCommits.Dequeue();
				// decrease stickiness
				_streamVersions.Put(
					commitInfo.StreamId,
					x => {
						if (!Debugger.IsAttached) Debugger.Launch();
						else Debugger.Break();
						throw new Exception(string.Format("CommitInfo for stream '{0}' is not present!", x));
					},
					(streamId, oldVersion) => oldVersion,
					stickiness: -1);
				if (SystemStreams.IsMetastream(commitInfo.StreamId)) {
					_streamRawMetas.Put(
						SystemStreams.OriginalStreamOf(commitInfo.StreamId),
						x => {
							if (!Debugger.IsAttached) Debugger.Launch();
							else Debugger.Break();
							throw new Exception(string.Format(
								"Original stream CommitInfo for meta-stream '{0}' is not present!",
								SystemStreams.MetastreamOf(x)));
						},
						(streamId, oldVersion) => oldVersion,
						stickiness: -1);
				}
			}
		}

		public void PurgeNotProcessedTransactions(long checkpoint) {
			while (_notProcessedTrans.Count > 0 && _notProcessedTrans.Peek().LogPosition < checkpoint) {
				var transInfo = _notProcessedTrans.Dequeue();
				// decrease stickiness
				_transactionInfoCache.Put(
					transInfo.TransactionId,
					x => { throw new Exception(string.Format("TransInfo for transaction ID {0} is not present!", x)); },
					(streamId, oldTransInfo) => oldTransInfo,
					stickiness: -1);
			}
		}

		private IEnumerable<PrepareLogRecord> GetTransactionPrepares(long transactionPos, long commitPos) {
			using (var reader = _indexBackend.BorrowReader()) {
				reader.Reposition(transactionPos);

				// in case all prepares were scavenged, we should not read past Commit LogPosition
				SeqReadResult result;
				while ((result = reader.TryReadNext()).Success && result.RecordPrePosition <= commitPos) {
					if (result.LogRecord.RecordType != LogRecordType.Prepare)
						continue;

					var prepare = (PrepareLogRecord)result.LogRecord;
					if (prepare.TransactionPosition == transactionPos) {
						yield return prepare;
						if (prepare.Flags.HasAnyOf(PrepareFlags.TransactionEnd))
							yield break;
					}
				}
			}
		}

		public bool IsSoftDeleted(string streamId) {
			return GetStreamMetadata(streamId).TruncateBefore == EventNumber.DeletedStream;
		}

		public long GetStreamLastEventNumber(string streamId) {
			long lastEventNumber;
			if (_streamVersions.TryGet(streamId, out lastEventNumber))
				return lastEventNumber;
			return _indexReader.GetStreamLastEventNumber(streamId);
		}

		public StreamMetadata GetStreamMetadata(string streamId) {
			StreamMeta meta;
			if (_streamRawMetas.TryGet(streamId, out meta)) {
				if (meta.Meta != null)
					return meta.Meta;
				var m = Helper.EatException(() => StreamMetadata.FromJsonBytes(meta.RawMeta), StreamMetadata.Empty);
				_streamRawMetas.Put(streamId, new StreamMeta(meta.RawMeta, m), 0);
				return m;
			}

			return _indexReader.GetStreamMetadata(streamId);
		}

		public RawMetaInfo GetStreamRawMeta(string streamId) {
			var metastreamId = SystemStreams.MetastreamOf(streamId);
			var metaLastEventNumber = GetStreamLastEventNumber(metastreamId);

			StreamMeta meta;
			if (!_streamRawMetas.TryGet(streamId, out meta))
				meta = new StreamMeta(_indexReader.ReadPrepare(metastreamId, metaLastEventNumber).Data, null);

			return new RawMetaInfo(metaLastEventNumber, meta.RawMeta);
		}

		private struct StreamMeta {
			public readonly byte[] RawMeta;
			public readonly StreamMetadata Meta;

			public StreamMeta(byte[] rawMeta, StreamMetadata meta) {
				RawMeta = rawMeta;
				Meta = meta;
			}
		}

		private struct EventInfo {
			public readonly string StreamId;
			public readonly long EventNumber;

			public EventInfo(string streamId, long eventNumber) {
				StreamId = streamId;
				EventNumber = eventNumber;
			}
		}

		private struct TransInfo {
			public readonly long TransactionId;
			public readonly long LogPosition;

			public TransInfo(long transactionId, long logPosition) {
				TransactionId = transactionId;
				LogPosition = logPosition;
			}
		}

		private struct CommitInfo {
			public readonly string StreamId;
			public readonly long LogPosition;

			public CommitInfo(string streamId, long logPosition) {
				StreamId = streamId;
				LogPosition = logPosition;
			}
		}
	}
}
