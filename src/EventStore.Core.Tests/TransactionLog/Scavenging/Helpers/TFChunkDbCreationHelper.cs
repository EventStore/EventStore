using System;
using System.Collections.Generic;
using System.IO;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.LogAbstraction;
using EventStore.Core.LogV2;
using EventStore.Core.Services;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.LogCommon;

namespace EventStore.Core.Tests.TransactionLog.Scavenging.Helpers {
	public class TFChunkDbCreationHelper<TLogFormat, TStreamId> {
		private readonly TFChunkDbConfig _dbConfig;
		private readonly TFChunkDb _db;

		private readonly List<Rec[]> _chunkRecs = new List<Rec[]>();

		private bool _completeLast;

		private static LogFormatAbstractor<TStreamId> _logFormat = LogFormatHelper<TLogFormat, TStreamId>.LogFormat;

		public TFChunkDbCreationHelper(TFChunkDbConfig dbConfig) {
			Ensure.NotNull(dbConfig, "dbConfig");
			_dbConfig = dbConfig;

			_db = new TFChunkDb(_dbConfig);
			_db.Open();

			if (_db.Config.WriterCheckpoint.ReadNonFlushed() > 0)
				throw new Exception("The DB already contains some data.");
		}

		public TFChunkDbCreationHelper<TLogFormat, TStreamId> Chunk(params Rec[] records) {
			_chunkRecs.Add(records);
			return this;
		}

		public TFChunkDbCreationHelper<TLogFormat, TStreamId> CompleteLastChunk() {
			_completeLast = true;
			return this;
		}

		public DbResult CreateDb() {
			var records = new ILogRecord[_chunkRecs.Count][];
			for (int i = 0; i < records.Length; ++i) {
				records[i] = new ILogRecord[_chunkRecs[i].Length];
			}

			var transactions = new Dictionary<int, TransactionInfo>();
			var streams = new Dictionary<string, StreamInfo>();
			var streamUncommitedVersion = new Dictionary<string, long>();

			for (int i = 0; i < _chunkRecs.Count; ++i) {
				for (int j = 0; j < _chunkRecs[i].Length; ++j) {
					var rec = _chunkRecs[i][j];

					if (!streams.ContainsKey(rec.StreamId)) {
						streams[rec.StreamId] = new StreamInfo(-1);
						streamUncommitedVersion[rec.StreamId] = -1;
					}

					if (!rec.Transaction.HasValue) {
						continue;
					}

					TransactionInfo transInfo;
					bool transCreate = transactions.TryGetValue(rec.Transaction.Value, out transInfo);
					if (!transCreate) {
						if (rec.Type == Rec.RecType.TransCommit)
							throw new Exception("Commit for non-existing transaction.");

						transactions[rec.Transaction.Value] = transInfo = new TransactionInfo(rec.StreamId, rec.Id, rec.Id);
					} else {
						if (rec.Type == Rec.RecType.TransStart)
							throw new Exception(string.Format("Unexpected record type: {0}.", rec.Type));
					}

					if (transInfo.StreamId != rec.StreamId) {
						throw new Exception(string.Format(
							"Wrong stream id for transaction. Transaction StreamId: {0}, record StreamId: {1}.",
							transInfo.StreamId,
							rec.StreamId));
					}

					transInfo.LastPrepareId = rec.Id;
				}
			}

			for (int i = 0; i < _chunkRecs.Count; ++i) {
				var chunk = i == 0 ? _db.Manager.GetChunk(0) : _db.Manager.AddNewChunk();
				_db.Config.WriterCheckpoint.Write(i * (long)_db.Config.ChunkSize);

				for (int j = 0; j < _chunkRecs[i].Length; ++j) {
					var rec = _chunkRecs[i][j];
					var transInfo = rec.Transaction.HasValue ? transactions[rec.Transaction.Value] :
							new TransactionInfo(rec.StreamId, rec.Id, rec.Id);
					var logPos = _db.Config.WriterCheckpoint.ReadNonFlushed();

					long streamVersion = streamUncommitedVersion[rec.StreamId];
					if (streamVersion == -1
					    && rec.Type != Rec.RecType.TransStart
					    && rec.Type != Rec.RecType.TransPrepare
					    && rec.Type != Rec.RecType.Prepare
					    && rec.Type != Rec.RecType.Delete) {
						throw new Exception(string.Format("Stream {0} is empty.", rec.StreamId));
					}

					if (streamVersion == EventNumber.DeletedStream && rec.Type != Rec.RecType.TransCommit)
						throw new Exception(string.Format(
							"Stream {0} was deleted, but we need to write some more prepares.", rec.StreamId));

					if (transInfo.FirstPrepareId == rec.Id) {
						transInfo.TransactionPosition = logPos;
						transInfo.TransactionEventNumber = streamVersion + 1;
						transInfo.TransactionOffset = 0;
					}

					ILogRecord record;

					var expectedVersion = transInfo.FirstPrepareId == rec.Id ? streamVersion : ExpectedVersion.Any;
					switch (rec.Type) {
						case Rec.RecType.Prepare: {
							record = CreateLogRecord(rec, transInfo, logPos, expectedVersion);

							if (SystemStreams.IsMetastream(rec.StreamId))
								transInfo.StreamMetadata = rec.StreamMetadata;

							streamUncommitedVersion[rec.StreamId] += 1;

							if (transInfo.StreamMetadata != null) {
								var streamId = SystemStreams.OriginalStreamOf(rec.StreamId);
								if (!streams.ContainsKey(streamId))
									streams.Add(streamId, new StreamInfo(-1));
								streams[streamId].StreamMetadata = transInfo.StreamMetadata;
							}

							streams[rec.StreamId].StreamVersion =
								transInfo.TransactionEventNumber + transInfo.TransactionOffset - 1;

							break;
						}
						case Rec.RecType.Delete: {
							record = CreateLogRecord(rec, transInfo, logPos, expectedVersion);
							streamUncommitedVersion[rec.StreamId] = EventNumber.DeletedStream;
							streams[rec.StreamId].StreamVersion = EventNumber.DeletedStream;
							break;
						}
						case Rec.RecType.TransPrepare: {
							record = CreateLogRecord(rec, transInfo, logPos, expectedVersion);

							if (SystemStreams.IsMetastream(rec.StreamId))
								transInfo.StreamMetadata = rec.StreamMetadata;

							streamUncommitedVersion[rec.StreamId] += 1;
							break;
						}
						case Rec.RecType.TransStart:
						case Rec.RecType.TransEnd: {
							record = CreateLogRecord(rec, transInfo, logPos, expectedVersion);
							break;
						}
						case Rec.RecType.TransCommit: {
							record = CreateLogRecord(rec, transInfo, logPos, expectedVersion);

							if (transInfo.StreamMetadata != null) {
								var streamId = SystemStreams.OriginalStreamOf(rec.StreamId);
								if (!streams.ContainsKey(streamId))
									streams.Add(streamId, new StreamInfo(-1));
								streams[streamId].StreamMetadata = transInfo.StreamMetadata;
							}

							streams[rec.StreamId].StreamVersion =
								transInfo.TransactionEventNumber + transInfo.TransactionOffset - 1;
							break;
						}
						default:
							throw new ArgumentOutOfRangeException();
					}

					var writerRes = chunk.TryAppend(record);
					if (!writerRes.Success)
						throw new Exception(string.Format("Could not write log record: {0}", record));
					_db.Config.WriterCheckpoint.Write(i * (long)_db.Config.ChunkSize + writerRes.NewPosition);

					records[i][j] = record;
				}

				if (i < _chunkRecs.Count - 1 || (_completeLast && i == _chunkRecs.Count - 1))
					chunk.Complete();
				else
					chunk.Flush();
			}

			return new DbResult(_db, records, streams);
		}

		private byte[] FormatStreamMetadata(Rec rec) {
			if (rec.StreamMetadata == null) return null;

			var meta = rec.StreamMetadata;
			return meta.ToJsonBytes();
		}

		private ILogRecord CreateLogRecord(Rec rec, TransactionInfo transInfo, long logPos, long expectedVersion) {
			switch (rec.Type) {
				case Rec.RecType.Prepare: {
					int transOffset = transInfo.TransactionOffset;
					transInfo.TransactionOffset += 1;

					_logFormat.StreamNameIndex.GetOrAddId(rec.StreamId, out var streamId);

					var flags = rec.PrepareFlags;
					byte[] data;
					if (SystemStreams.IsMetastream(rec.StreamId) && rec.StreamMetadata != null) {
						data = FormatStreamMetadata(rec);
						flags |= PrepareFlags.IsJson;
					} else {
						data = rec.Id.ToByteArray();
					}

					return LogRecord.Prepare(_logFormat.RecordFactory, logPos,
						Guid.NewGuid(),
						rec.Id,
						transInfo.TransactionPosition,
						transOffset,
						streamId,
						expectedVersion,
						flags | PrepareFlags.SingleWrite | PrepareFlags.IsCommitted,
						rec.EventType,
						data,
						null,
						rec.TimeStamp);
				}
				case Rec.RecType.Delete: {
					int transOffset = transInfo.TransactionOffset;
					transInfo.TransactionOffset += 1;

					_logFormat.StreamNameIndex.GetOrAddId(rec.StreamId, out var streamId);
					return LogRecord.Prepare(_logFormat.RecordFactory, logPos,
						Guid.NewGuid(),
						rec.Id,
						transInfo.TransactionPosition,
						transOffset,
						streamId,
						EventNumber.DeletedStream - 1,
						rec.PrepareFlags | PrepareFlags.DeleteTombstone | PrepareFlags.IsCommitted,
						SystemEventTypes.StreamDeleted,
						LogRecord.NoData,
						null,
						rec.TimeStamp);
				}
				case Rec.RecType.TransPrepare: {
					int transOffset = transInfo.TransactionOffset;
					transInfo.TransactionOffset += 1;
					_logFormat.StreamNameIndex.GetOrAddId(rec.StreamId, out var streamId);

					var flags = rec.PrepareFlags;
					byte[] data;
					if (SystemStreams.IsMetastream(rec.StreamId) && rec.StreamMetadata != null) {
						data = FormatStreamMetadata(rec);
						flags |= PrepareFlags.IsJson;
					} else {
						data = rec.Id.ToByteArray();
					}

					return LogRecord.Prepare(_logFormat.RecordFactory, logPos,
						Guid.NewGuid(),
						rec.Id,
						transInfo.TransactionPosition,
						transOffset,
						streamId,
						expectedVersion,
						flags
						| PrepareFlags.Data
						| (transInfo.FirstPrepareId == rec.Id ? PrepareFlags.TransactionBegin : PrepareFlags.None)
						| (transInfo.LastPrepareId == rec.Id ? PrepareFlags.TransactionEnd : PrepareFlags.None),
						rec.EventType,
						data,
						null,
						rec.TimeStamp);
				}
				case Rec.RecType.TransStart:
				case Rec.RecType.TransEnd: {
					_logFormat.StreamNameIndex.GetOrAddId(rec.StreamId, out var streamId);
					return LogRecord.Prepare(_logFormat.RecordFactory, logPos,
						Guid.NewGuid(),
						rec.Id,
						transInfo.TransactionPosition,
						-1,
						streamId,
						expectedVersion,
						(transInfo.FirstPrepareId == rec.Id ? PrepareFlags.TransactionBegin : PrepareFlags.None)
						| (transInfo.LastPrepareId == rec.Id ? PrepareFlags.TransactionEnd : PrepareFlags.None),
						rec.EventType,
						LogRecord.NoData,
						LogRecord.NoData,
						rec.TimeStamp);
				}
				case Rec.RecType.TransCommit: {
					return LogRecord.Commit(logPos, Guid.NewGuid(), transInfo.TransactionPosition,
						transInfo.TransactionEventNumber);
				}
				default:
					throw new ArgumentOutOfRangeException();
			}
		}
	}

	public class TransactionInfo {
		public readonly string StreamId;
		public readonly Guid FirstPrepareId;
		public Guid LastPrepareId;
		public long TransactionPosition = -1;
		public int TransactionOffset = 0;
		public long TransactionEventNumber = -1;
		public StreamMetadata StreamMetadata;

		public TransactionInfo(string streamId, Guid firstPrepareId, Guid lastPrepareId) {
			StreamId = streamId;
			FirstPrepareId = firstPrepareId;
			LastPrepareId = lastPrepareId;
		}
	}

	public class StreamInfo {
		public long StreamVersion;
		public StreamMetadata StreamMetadata;

		public StreamInfo(long streamVersion) {
			StreamVersion = streamVersion;
		}
	}

	public class DbResult {
		public readonly TFChunkDb Db;
		public readonly ILogRecord[][] Recs;
		public readonly Dictionary<string, StreamInfo> Streams;

		public DbResult(TFChunkDb db, ILogRecord[][] recs, Dictionary<string, StreamInfo> streams) {
			Ensure.NotNull(db, "db");
			Ensure.NotNull(recs, "recs");
			Ensure.NotNull(streams, "streams");

			Db = db;
			Recs = recs;
			Streams = streams;
		}
	}

	public class Rec {
		public enum RecType {
			Prepare,
			Delete,
			TransStart,
			TransPrepare,
			TransEnd,
			TransCommit
		}

		public readonly RecType Type;
		public readonly Guid Id;
		public readonly int? Transaction;
		public readonly string StreamId;
		public readonly string EventType;
		public readonly DateTime TimeStamp;
		public readonly StreamMetadata StreamMetadata;
		public readonly PrepareFlags PrepareFlags;

		public Rec(RecType type, int? transaction, string streamId, string eventType, DateTime? timestamp,
			StreamMetadata streamMetadata = null, PrepareFlags prepareFlags = PrepareFlags.None) {
			Ensure.NotNullOrEmpty(streamId, "streamId");
			if (transaction.HasValue) {
				Ensure.Nonnegative(transaction.Value, "transaction");
			}

			Type = type;
			Id = Guid.NewGuid();
			Transaction = transaction;
			StreamId = streamId;
			EventType = eventType ?? string.Empty;
			TimeStamp = timestamp ?? DateTime.UtcNow;
			StreamMetadata = streamMetadata;
			PrepareFlags = prepareFlags;
		}

		public static Rec Prepare(string stream, string eventType = null, DateTime? timestamp = null,
			StreamMetadata streamMetadata = null) {
			return new(RecType.Prepare, null, stream, eventType, timestamp, streamMetadata);
		}

		public static Rec Delete(string stream, DateTime? timestamp = null) {
			return new(RecType.Delete, null, stream, null, timestamp);
		}

		public static Rec TransStart(int transaction, string stream, DateTime? timestamp = null) {
			return new(RecType.TransStart, transaction, stream, null, timestamp);
		}

		public static Rec TransPrepare(int transaction, string stream, string eventType = null, DateTime? timestamp = null,
			StreamMetadata streamMetadata = null) {
			return new(RecType.TransPrepare, transaction, stream, eventType, timestamp, streamMetadata);
		}

		public static Rec TransEnd(int transaction, string stream, DateTime? timestamp = null) {
			return new(RecType.TransEnd, transaction, stream, null, timestamp);
		}

		public static Rec TransCommit(int transaction, string stream, DateTime? timestamp = null) {
			return new(RecType.TransCommit, transaction, stream, null, timestamp);
		}
	}
}
