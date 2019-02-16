using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Text;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Index;
using EventStore.Core.Index.Hashes;
using EventStore.Core.Messages;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Services.Storage.ReaderIndex {
	public interface IIndexCommitter {
		long LastCommitPosition { get; }
		void Init(long buildToPosition);
		void Dispose();
		long Commit(CommitLogRecord commit, bool isTfEof, bool cacheLastEventNumber);
		long Commit(IList<PrepareLogRecord> commitedPrepares, bool isTfEof, bool cacheLastEventNumber);
		long GetCommitLastEventNumber(CommitLogRecord commit);
	}

	public class IndexCommitter : IIndexCommitter {
		public static readonly ILogger Log = LogManager.GetLoggerFor<IndexCommitter>();

		public long LastCommitPosition {
			get { return Interlocked.Read(ref _lastCommitPosition); }
		}

		private readonly IPublisher _bus;
		private readonly IIndexBackend _backend;
		private readonly IIndexReader _indexReader;
		private readonly ITableIndex _tableIndex;
		private readonly bool _additionalCommitChecks;
		private long _persistedPreparePos = -1;
		private long _persistedCommitPos = -1;
		private bool _indexRebuild = true;
		private long _lastCommitPosition = -1;

		public IndexCommitter(IPublisher bus, IIndexBackend backend, IIndexReader indexReader,
			ITableIndex tableIndex, bool additionalCommitChecks) {
			_bus = bus;
			_backend = backend;
			_indexReader = indexReader;
			_tableIndex = tableIndex;
			_additionalCommitChecks = additionalCommitChecks;
		}

		public void Init(long buildToPosition) {
			Log.Info("TableIndex initialization...");

			_tableIndex.Initialize(buildToPosition);
			_persistedPreparePos = _tableIndex.PrepareCheckpoint;
			_persistedCommitPos = _tableIndex.CommitCheckpoint;
			_lastCommitPosition = _tableIndex.CommitCheckpoint;

			if (_lastCommitPosition >= buildToPosition)
				throw new Exception(string.Format("_lastCommitPosition {0} >= buildToPosition {1}", _lastCommitPosition,
					buildToPosition));

			var startTime = DateTime.UtcNow;
			var lastTime = DateTime.UtcNow;
			var reportPeriod = TimeSpan.FromSeconds(5);

			Log.Info("ReadIndex building...");

			_indexRebuild = true;
			using (var reader = _backend.BorrowReader()) {
				var startPosition = Math.Max(0, _persistedCommitPos);
				reader.Reposition(startPosition);

				var commitedPrepares = new List<PrepareLogRecord>();

				long processed = 0;
				SeqReadResult result;
				while ((result = reader.TryReadNext()).Success && result.LogRecord.LogPosition < buildToPosition) {
					switch (result.LogRecord.RecordType) {
						case LogRecordType.Prepare: {
							var prepare = (PrepareLogRecord)result.LogRecord;
							if (prepare.Flags.HasAnyOf(PrepareFlags.IsCommitted)) {
								if (prepare.Flags.HasAnyOf(PrepareFlags.SingleWrite)) {
									Commit(commitedPrepares, false, false);
									commitedPrepares.Clear();
									Commit(new[] {prepare}, result.Eof, false);
								} else {
									if (prepare.Flags.HasAnyOf(PrepareFlags.Data | PrepareFlags.StreamDelete))
										commitedPrepares.Add(prepare);
									if (prepare.Flags.HasAnyOf(PrepareFlags.TransactionEnd)) {
										Commit(commitedPrepares, result.Eof, false);
										commitedPrepares.Clear();
									}
								}
							}

							break;
						}
						case LogRecordType.Commit:
							Commit((CommitLogRecord)result.LogRecord, result.Eof, false);
							break;
						case LogRecordType.System:
							break;
						default:
							throw new Exception(string.Format("Unknown RecordType: {0}", result.LogRecord.RecordType));
					}

					processed += 1;
					if (DateTime.UtcNow - lastTime > reportPeriod || processed % 100000 == 0) {
						Log.Debug("ReadIndex Rebuilding: processed {processed} records ({resultPosition:0.0}%).",
							processed,
							(result.RecordPostPosition - startPosition) * 100.0 / (buildToPosition - startPosition));
						lastTime = DateTime.UtcNow;
					}
				}

				Log.Debug("ReadIndex rebuilding done: total processed {processed} records, time elapsed: {elapsed}.",
					processed, DateTime.UtcNow - startTime);
				_bus.Publish(new StorageMessage.TfEofAtNonCommitRecord());
				_backend.SetSystemSettings(GetSystemSettings());
			}

			_indexRebuild = false;
		}

		public void Dispose() {
			try {
				_tableIndex.Close(removeFiles: false);
			} catch (TimeoutException exc) {
				Log.ErrorException(exc, "Timeout exception when trying to close TableIndex.");
				throw;
			}
		}

		public long GetCommitLastEventNumber(CommitLogRecord commit) {
			long eventNumber = EventNumber.Invalid;

			var lastCommitPosition = Interlocked.Read(ref _lastCommitPosition);
			if (commit.LogPosition < lastCommitPosition || (commit.LogPosition == lastCommitPosition && !_indexRebuild))
				return eventNumber;

			foreach (var prepare in GetTransactionPrepares(commit.TransactionPosition, commit.LogPosition)) {
				if (prepare.Flags.HasNoneOf(PrepareFlags.StreamDelete | PrepareFlags.Data))
					continue;
				eventNumber = prepare.Flags.HasAllOf(PrepareFlags.StreamDelete)
					? EventNumber.DeletedStream
					: commit.FirstEventNumber + prepare.TransactionOffset;
			}

			return eventNumber;
		}

		public long Commit(CommitLogRecord commit, bool isTfEof, bool cacheLastEventNumber) {
			long eventNumber = EventNumber.Invalid;

			var lastCommitPosition = Interlocked.Read(ref _lastCommitPosition);
			if (commit.LogPosition < lastCommitPosition || (commit.LogPosition == lastCommitPosition && !_indexRebuild))
				return eventNumber; // already committed

			string streamId = null;
			var indexEntries = new List<IndexKey>();
			var prepares = new List<PrepareLogRecord>();

			foreach (var prepare in GetTransactionPrepares(commit.TransactionPosition, commit.LogPosition)) {
				if (prepare.Flags.HasNoneOf(PrepareFlags.StreamDelete | PrepareFlags.Data))
					continue;

				if (streamId == null) {
					streamId = prepare.EventStreamId;
				} else {
					if (prepare.EventStreamId != streamId)
						throw new Exception(string.Format("Expected stream: {0}, actual: {1}. LogPosition: {2}",
							streamId, prepare.EventStreamId, commit.LogPosition));
				}

				eventNumber = prepare.Flags.HasAllOf(PrepareFlags.StreamDelete)
					? EventNumber.DeletedStream
					: commit.FirstEventNumber + prepare.TransactionOffset;

				if (new TFPos(commit.LogPosition, prepare.LogPosition) >
				    new TFPos(_persistedCommitPos, _persistedPreparePos)) {
					indexEntries.Add(new IndexKey(streamId, eventNumber, prepare.LogPosition));
					prepares.Add(prepare);
				}
			}

			if (indexEntries.Count > 0) {
				if (_additionalCommitChecks && cacheLastEventNumber) {
					CheckStreamVersion(streamId, indexEntries[0].Version, commit);
					CheckDuplicateEvents(streamId, commit, indexEntries, prepares);
				}

				_tableIndex.AddEntries(commit.LogPosition, indexEntries); // atomically add a whole bulk of entries
			}

			if (eventNumber != EventNumber.Invalid) {
				if (eventNumber < 0) throw new Exception(string.Format("EventNumber {0} is incorrect.", eventNumber));

				if (cacheLastEventNumber) {
					_backend.SetStreamLastEventNumber(streamId, eventNumber);
				}

				if (SystemStreams.IsMetastream(streamId))
					_backend.SetStreamMetadata(SystemStreams.OriginalStreamOf(streamId),
						null); // invalidate cached metadata

				if (streamId == SystemStreams.SettingsStream)
					_backend.SetSystemSettings(DeserializeSystemSettings(prepares[prepares.Count - 1].Data));
			}

			var newLastCommitPosition = Math.Max(commit.LogPosition, lastCommitPosition);
			if (Interlocked.CompareExchange(ref _lastCommitPosition, newLastCommitPosition, lastCommitPosition) !=
			    lastCommitPosition)
				throw new Exception(
					"Concurrency error in ReadIndex.Commit: _lastCommitPosition was modified during Commit execution!");

			if (!_indexRebuild)
				for (int i = 0, n = indexEntries.Count; i < n; ++i) {
					_bus.Publish(
						new StorageMessage.EventCommitted(
							commit.LogPosition,
							new EventRecord(indexEntries[i].Version, prepares[i]),
							isTfEof && i == n - 1));
				}

			return eventNumber;
		}

		public long Commit(IList<PrepareLogRecord> commitedPrepares, bool isTfEof, bool cacheLastEventNumber) {
			long eventNumber = EventNumber.Invalid;

			if (commitedPrepares.Count == 0)
				return eventNumber;

			var lastCommitPosition = Interlocked.Read(ref _lastCommitPosition);
			var lastPrepare = commitedPrepares[commitedPrepares.Count - 1];

			string streamId = lastPrepare.EventStreamId;
			var indexEntries = new List<IndexKey>();
			var prepares = new List<PrepareLogRecord>();

			foreach (var prepare in commitedPrepares) {
				if (prepare.Flags.HasNoneOf(PrepareFlags.StreamDelete | PrepareFlags.Data))
					continue;

				if (prepare.EventStreamId != streamId) {
					var sb = new StringBuilder();
					sb.Append(string.Format("ERROR: Expected stream: {0}, actual: {1}.", streamId,
						prepare.EventStreamId));
					sb.Append(Environment.NewLine);
					sb.Append(Environment.NewLine);
					sb.Append("Prepares: (" + commitedPrepares.Count + ")");
					sb.Append(Environment.NewLine);
					for (int i = 0; i < commitedPrepares.Count; i++) {
						var p = commitedPrepares[i];
						sb.Append("Stream ID: " + p.EventStreamId);
						sb.Append(Environment.NewLine);
						sb.Append("LogPosition: " + p.LogPosition);
						sb.Append(Environment.NewLine);
						sb.Append("Flags: " + p.Flags);
						sb.Append(Environment.NewLine);
						sb.Append("Type: " + p.EventType);
						sb.Append(Environment.NewLine);
						sb.Append("MetaData: " + Encoding.UTF8.GetString(p.Metadata));
						sb.Append(Environment.NewLine);
						sb.Append("Data: " + Encoding.UTF8.GetString(p.Data));
						sb.Append(Environment.NewLine);
					}

					throw new Exception(sb.ToString());
				}

				if (prepare.LogPosition < lastCommitPosition ||
				    (prepare.LogPosition == lastCommitPosition && !_indexRebuild))
					continue; // already committed

				eventNumber =
					prepare.ExpectedVersion + 1; /* for committed prepare expected version is always explicit */

				if (new TFPos(prepare.LogPosition, prepare.LogPosition) >
				    new TFPos(_persistedCommitPos, _persistedPreparePos)) {
					indexEntries.Add(new IndexKey(streamId, eventNumber, prepare.LogPosition));
					prepares.Add(prepare);
				}
			}

			if (indexEntries.Count > 0) {
				if (_additionalCommitChecks && cacheLastEventNumber) {
					CheckStreamVersion(streamId, indexEntries[0].Version, null); // TODO AN: bad passing null commit
					CheckDuplicateEvents(streamId, null, indexEntries, prepares); // TODO AN: bad passing null commit
				}

				_tableIndex.AddEntries(lastPrepare.LogPosition, indexEntries); // atomically add a whole bulk of entries
			}

			if (eventNumber != EventNumber.Invalid) {
				if (eventNumber < 0) throw new Exception(string.Format("EventNumber {0} is incorrect.", eventNumber));

				if (cacheLastEventNumber) {
					_backend.SetStreamLastEventNumber(streamId, eventNumber);
				}

				if (SystemStreams.IsMetastream(streamId))
					_backend.SetStreamMetadata(SystemStreams.OriginalStreamOf(streamId),
						null); // invalidate cached metadata

				if (streamId == SystemStreams.SettingsStream)
					_backend.SetSystemSettings(DeserializeSystemSettings(prepares[prepares.Count - 1].Data));
			}

			var newLastCommitPosition = Math.Max(lastPrepare.LogPosition, lastCommitPosition);
			if (Interlocked.CompareExchange(ref _lastCommitPosition, newLastCommitPosition, lastCommitPosition) !=
			    lastCommitPosition)
				throw new Exception(
					"Concurrency error in ReadIndex.Commit: _lastCommitPosition was modified during Commit execution!");

			if (!_indexRebuild)
				for (int i = 0, n = indexEntries.Count; i < n; ++i) {
					_bus.Publish(
						new StorageMessage.EventCommitted(
							prepares[i].LogPosition,
							new EventRecord(indexEntries[i].Version, prepares[i]),
							isTfEof && i == n - 1));
				}

			return eventNumber;
		}

		private IEnumerable<PrepareLogRecord> GetTransactionPrepares(long transactionPos, long commitPos) {
			using (var reader = _backend.BorrowReader()) {
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

		private void CheckStreamVersion(string streamId, long newEventNumber, CommitLogRecord commit) {
			if (newEventNumber == EventNumber.DeletedStream)
				return;

			long lastEventNumber = _indexReader.GetStreamLastEventNumber(streamId);
			if (newEventNumber != lastEventNumber + 1) {
				if (Debugger.IsAttached)
					Debugger.Break();
				else
					throw new Exception(
						string.Format(
							"Commit invariant violation: new event number {0} does not correspond to current stream version {1}.\n"
							+ "Stream ID: {2}.\nCommit: {3}.", newEventNumber, lastEventNumber, streamId, commit));
			}
		}

		private void CheckDuplicateEvents(string streamId, CommitLogRecord commit, IList<IndexKey> indexEntries,
			IList<PrepareLogRecord> prepares) {
			using (var reader = _backend.BorrowReader()) {
				var entries = _tableIndex.GetRange(streamId, indexEntries[0].Version,
					indexEntries[indexEntries.Count - 1].Version);
				foreach (var indexEntry in entries) {
					int prepareIndex = (int)(indexEntry.Version - indexEntries[0].Version);
					var prepare = prepares[prepareIndex];
					PrepareLogRecord indexedPrepare = GetPrepare(reader, indexEntry.Position);
					if (indexedPrepare != null && indexedPrepare.EventStreamId == prepare.EventStreamId) {
						if (Debugger.IsAttached)
							Debugger.Break();
						else
							throw new Exception(
								string.Format("Trying to add duplicate event #{0} to stream {1} \nCommit: {2}\n"
								              + "Prepare: {3}\nIndexed prepare: {4}.",
									indexEntry.Version, prepare.EventStreamId, commit, prepare, indexedPrepare));
					}
				}
			}
		}

		private SystemSettings GetSystemSettings() {
			var res = _indexReader.ReadEvent(SystemStreams.SettingsStream, -1);
			return res.Result == ReadEventResult.Success ? DeserializeSystemSettings(res.Record.Data) : null;
		}

		private static SystemSettings DeserializeSystemSettings(byte[] settingsData) {
			try {
				return SystemSettings.FromJsonBytes(settingsData);
			} catch (Exception exc) {
				Log.ErrorException(exc, "Error deserializing SystemSettings record.");
			}

			return null;
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
	}
}
