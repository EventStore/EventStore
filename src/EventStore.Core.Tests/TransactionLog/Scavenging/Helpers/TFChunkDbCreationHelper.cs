﻿using System;
using System.Collections.Generic;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Services;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Tests.TransactionLog.Scavenging.Helpers {
	public class TFChunkDbCreationHelper {
		private readonly TFChunkDbConfig _dbConfig;
		private readonly TFChunkDb _db;

		private readonly List<Rec[]> _chunkRecs = new List<Rec[]>();

		private bool _completeLast;

		public TFChunkDbCreationHelper(TFChunkDbConfig dbConfig) {
			Ensure.NotNull(dbConfig, "dbConfig");
			_dbConfig = dbConfig;

			_db = new TFChunkDb(_dbConfig);
			_db.Open();

			if (_db.Config.WriterCheckpoint.ReadNonFlushed() > 0)
				throw new Exception("The DB already contains some data.");
		}

		public TFChunkDbCreationHelper Chunk(params Rec[] records) {
			_chunkRecs.Add(records);
			return this;
		}

		public TFChunkDbCreationHelper CompleteLastChunk() {
			_completeLast = true;
			return this;
		}

		public DbResult CreateDb() {
			var records = new LogRecord[_chunkRecs.Count][];
			for (int i = 0; i < records.Length; ++i) {
				records[i] = new LogRecord[_chunkRecs[i].Length];
			}

			var transactions = new Dictionary<int, TransactionInfo>();
			var streams = new Dictionary<string, StreamInfo>();
			var streamUncommitedVersion = new Dictionary<string, long>();

			// for each chunk i
			for (int i = 0; i < _chunkRecs.Count; ++i) {
				// for each record j in chunk i
				for (int j = 0; j < _chunkRecs[i].Length; ++j) {
					var rec = _chunkRecs[i][j];

					TransactionInfo transInfo;
					bool transCreate = transactions.TryGetValue(rec.Transaction, out transInfo);
					if (!transCreate) {
						if (rec.Type == Rec.RecType.Commit)
							throw new Exception("Commit for non-existing transaction.");

						transactions[rec.Transaction] = transInfo = new TransactionInfo(rec.StreamId, rec.Id, rec.Id);

						streams[rec.StreamId] = new StreamInfo(-1);
						streamUncommitedVersion[rec.StreamId] = -1;
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

					if (rec.Type != Rec.RecType.Commit && transInfo.IsDelete)
						throw new Exception("Transaction with records after delete record.");

					if (rec.Type == Rec.RecType.Delete)
						transInfo.IsDelete = true;

					transInfo.LastPrepareId = rec.Id;
				}
			}

			// convert the Recs into LogRecords and write them to the database.
			// for each chunk i
			for (int i = 0; i < _chunkRecs.Count; ++i) {
				var chunk = i == 0 ? _db.Manager.GetChunk(0) : _db.Manager.AddNewChunk();
				_db.Config.WriterCheckpoint.Write(i * (long)_db.Config.ChunkSize);

				var completedChunk = false;
				// for each record j in chunk i
				for (int j = 0; j < _chunkRecs[i].Length; ++j) {
					if (completedChunk)
						throw new InvalidOperationException("Don't try to write more data to completed chunk");
					var rec = _chunkRecs[i][j];
					var transInfo = transactions[rec.Transaction];
					var logPos = _db.Config.WriterCheckpoint.ReadNonFlushed();

					long streamVersion = streamUncommitedVersion[rec.StreamId];

					if (rec.EventNumber.HasValue && rec.EventNumber > streamVersion + 1) {
						// advance the stream
						streamVersion = rec.EventNumber.Value - 1;
						streamUncommitedVersion[rec.StreamId] = streamVersion;
					}

					if (streamVersion == -1
					    && rec.Type != Rec.RecType.TransStart
					    && rec.Type != Rec.RecType.Prepare
					    && rec.Type != Rec.RecType.Delete) {
						throw new Exception(string.Format("Stream {0} is empty.", rec.StreamId));
					}

					if (streamVersion == EventNumber.DeletedStream && rec.Type != Rec.RecType.Commit)
						throw new Exception(string.Format(
							"Stream {0} was deleted, but we need to write some more prepares.", rec.StreamId));

					if (transInfo.FirstPrepareId == rec.Id) {
						transInfo.TransactionPosition = logPos;
						transInfo.TransactionEventNumber = streamVersion + 1;
						transInfo.TransactionOffset = 0;
					}

					LogRecord record;

					var expectedVersion =
						(rec.EventNumber - 1) ??
						(transInfo.FirstPrepareId == rec.Id ? streamVersion : ExpectedVersion.Any);

					switch (rec.Type) {
						case Rec.RecType.Prepare: {
							record = CreateLogRecord(rec, transInfo, logPos, expectedVersion);

							if (SystemStreams.IsMetastream(rec.StreamId))
								transInfo.StreamMetadata = rec.Metadata;

							streamUncommitedVersion[rec.StreamId] += 1;
							break;
						}

						case Rec.RecType.Delete: {
							if (rec.PrepareFlags.HasAnyOf(PrepareFlags.IsCommitted)) {
								// modern style tombstone has IsCommitted flag and max event number.
								// introduced in 19f7cd351329f7bd2be0f6c00b4beee45e2a4777
								expectedVersion = rec.Version == LogRecordVersion.LogRecordV0
									? int.MaxValue - 1
									: EventNumber.DeletedStream - 1;
							} else {
								// legacy style tombstone has 'normal' expected version in the log record
								// (probably -1 because it is committed separately)
								// a subsequent commit record to commit it
								// special handling in indexcommitter to give it a max event number in
								// the index.
							}

							record = CreateLogRecord(rec, transInfo, logPos, expectedVersion);

							streamUncommitedVersion[rec.StreamId] = rec.Version == LogRecordVersion.LogRecordV0
								? int.MaxValue
								: EventNumber.DeletedStream;
							break;
						}

						case Rec.RecType.TransStart:
						case Rec.RecType.TransEnd: {
							record = CreateLogRecord(rec, transInfo, logPos, expectedVersion);
							break;
						}
						case Rec.RecType.Commit: {
							record = CreateLogRecord(rec, transInfo, logPos, expectedVersion);

							if (transInfo.StreamMetadata != null) {
								var streamId = SystemStreams.OriginalStreamOf(rec.StreamId);
								if (!streams.ContainsKey(streamId))
									streams.Add(streamId, new StreamInfo(-1));
								streams[streamId].StreamMetadata = transInfo.StreamMetadata;
							}

							if (transInfo.IsDelete)
								streams[rec.StreamId].StreamVersion = EventNumber.DeletedStream;
							else
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

					if (record is PrepareLogRecord prepare && prepare.EventType == SystemEventTypes.ScavengePoint) {
						chunk.Complete();
						completedChunk = true;
					}

					records[i][j] = record;
				}

				if (!completedChunk) {
					if (i < _chunkRecs.Count - 1 || (_completeLast && i == _chunkRecs.Count - 1)) {
						chunk.Complete();
					} else {
						chunk.Flush();
					}
				}
			}

			_db.Config.WriterCheckpoint.Flush();
			return new DbResult(_db, records, streams);
		}

		private byte[] FormatData(Rec rec) {
			return rec.Data ?? rec.Metadata?.ToJsonBytes() ?? rec.Id.ToByteArray();
		}

		private LogRecord CreateLogRecord(Rec rec, TransactionInfo transInfo, long logPos, long expectedVersion) {
			switch (rec.Type) {
				case Rec.RecType.Prepare: {
					int transOffset = transInfo.TransactionOffset;
					transInfo.TransactionOffset += 1;

					if (rec.Version == LogRecordVersion.LogRecordV0) {
						return CreateLogRecordV0(rec, transInfo, transOffset, logPos, expectedVersion,
							FormatData(rec),
							rec.PrepareFlags
							| PrepareFlags.Data
							| (transInfo.FirstPrepareId == rec.Id ? PrepareFlags.TransactionBegin : PrepareFlags.None)
							| (transInfo.LastPrepareId == rec.Id ? PrepareFlags.TransactionEnd : PrepareFlags.None)
							| (rec.Metadata == null ? PrepareFlags.None : PrepareFlags.IsJson));
					}

					return LogRecord.Prepare(logPos,
						Guid.NewGuid(),
						rec.Id,
						transInfo.TransactionPosition,
						transOffset,
						rec.StreamId,
						expectedVersion,
						rec.PrepareFlags
						| (transInfo.FirstPrepareId == rec.Id ? PrepareFlags.TransactionBegin : PrepareFlags.None)
						| (transInfo.LastPrepareId == rec.Id ? PrepareFlags.TransactionEnd : PrepareFlags.None)
						| (rec.Metadata == null ? PrepareFlags.None : PrepareFlags.IsJson),
						rec.EventType,
						FormatData(rec),
						null,
						rec.TimeStamp);
				}

				case Rec.RecType.Delete: {
					int transOffset = transInfo.TransactionOffset;
					transInfo.TransactionOffset += 1;

					if (rec.Version == LogRecordVersion.LogRecordV0) {
						return CreateLogRecordV0(rec, transInfo, transOffset, logPos, expectedVersion,
							LogRecord.NoData,
							rec.PrepareFlags
							| PrepareFlags.StreamDelete
							| (transInfo.FirstPrepareId == rec.Id ? PrepareFlags.TransactionBegin : PrepareFlags.None)
							| (transInfo.LastPrepareId == rec.Id ? PrepareFlags.TransactionEnd : PrepareFlags.None));
					}

					return LogRecord.Prepare(logPos,
						Guid.NewGuid(),
						rec.Id,
						transInfo.TransactionPosition,
						transOffset,
						rec.StreamId,
						expectedVersion,
						rec.PrepareFlags
						| PrepareFlags.StreamDelete
						| (transInfo.FirstPrepareId == rec.Id ? PrepareFlags.TransactionBegin : PrepareFlags.None)
						| (transInfo.LastPrepareId == rec.Id ? PrepareFlags.TransactionEnd : PrepareFlags.None),
						rec.EventType,
						LogRecord.NoData,
						null,
						rec.TimeStamp);
				}

				case Rec.RecType.TransStart:
				case Rec.RecType.TransEnd: {
					if (rec.Version == LogRecordVersion.LogRecordV0) {
						return CreateLogRecordV0(rec, transInfo, -1, logPos, expectedVersion,
							LogRecord.NoData,
							(transInfo.FirstPrepareId == rec.Id ? PrepareFlags.TransactionBegin : PrepareFlags.None)
							| (transInfo.LastPrepareId == rec.Id ? PrepareFlags.TransactionEnd : PrepareFlags.None));
					}

					return LogRecord.Prepare(logPos,
						Guid.NewGuid(),
						rec.Id,
						transInfo.TransactionPosition,
						-1,
						rec.StreamId,
						expectedVersion,
						(transInfo.FirstPrepareId == rec.Id ? PrepareFlags.TransactionBegin : PrepareFlags.None)
						| (transInfo.LastPrepareId == rec.Id ? PrepareFlags.TransactionEnd : PrepareFlags.None),
						rec.EventType,
						LogRecord.NoData,
						null,
						rec.TimeStamp);
				}
				case Rec.RecType.Commit: {
					if (rec.Version == LogRecordVersion.LogRecordV0) {
						return new CommitLogRecord(logPos, Guid.NewGuid(), transInfo.TransactionPosition,
							DateTime.UtcNow, transInfo.TransactionEventNumber, LogRecordVersion.LogRecordV0);
					}

					return LogRecord.Commit(logPos, Guid.NewGuid(), transInfo.TransactionPosition,
						transInfo.TransactionEventNumber);
				}
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private LogRecord CreateLogRecordV0(Rec rec, TransactionInfo transInfo, int transOffset, long logPos,
			long expectedVersion, byte[] data, PrepareFlags flags) {
			return new PrepareLogRecord(logPos,
				Guid.NewGuid(),
				rec.Id,
				transInfo.TransactionPosition,
				transOffset,
				rec.StreamId,
				expectedVersion,
				rec.TimeStamp,
				flags,
				rec.EventType,
				data,
				null,
				LogRecordVersion.LogRecordV0);
		}
	}

	public class TransactionInfo {
		public readonly string StreamId;
		public readonly Guid FirstPrepareId;
		public Guid LastPrepareId;
		public long TransactionPosition = -1;
		public int TransactionOffset = 0;
		public long TransactionEventNumber = -1;
		public bool IsDelete = false;
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
		public TFChunkDb Db { get; }
		public LogRecord[][] Recs { get; }
		public Dictionary<string, StreamInfo> Streams { get; }

		public DbResult(TFChunkDb db, LogRecord[][] recs, Dictionary<string, StreamInfo> streams) {
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
			TransEnd,
			Commit
		}

		public readonly RecType Type;
		public readonly Guid Id;
		public readonly int Transaction;
		public readonly string StreamId;
		public readonly string EventType;
		public readonly DateTime TimeStamp;
		public readonly long? EventNumber;
		public readonly byte[] Data;
		public readonly StreamMetadata Metadata;
		public readonly PrepareFlags PrepareFlags;
		public readonly byte Version;

		public Rec(RecType type, int transaction, string streamId, string eventType, DateTime? timestamp, byte version,
			long? eventNumber = null,
			byte[] data = null,
			StreamMetadata metadata = null, PrepareFlags prepareFlags = PrepareFlags.Data) {
			Ensure.NotNullOrEmpty(streamId, "streamId");
			Ensure.Nonnegative(transaction, "transaction");

			if (data != null && metadata != null)
				throw new Exception("two kinds of data were specified");

			Type = type;
			Id = Guid.NewGuid();
			Transaction = transaction;
			StreamId = streamId;
			EventType = eventType ?? string.Empty;
			TimeStamp = timestamp ?? DateTime.UtcNow;
			Version = version;
			EventNumber = eventNumber;
			Data = data;
			Metadata = metadata;
			PrepareFlags = prepareFlags;
		}

		public static Rec Delete(int transaction, string stream, DateTime? timestamp = null,
			byte version = PrepareLogRecord.PrepareRecordVersion) {
			return new Rec(RecType.Delete, transaction, stream, SystemEventTypes.StreamDeleted, timestamp, version);
		}

		public static Rec CommittedDelete(int transaction, string stream, DateTime? timestamp = null,
			byte version = PrepareLogRecord.PrepareRecordVersion) {
			return new Rec(RecType.Delete, transaction, stream, SystemEventTypes.StreamDeleted, timestamp, version,
				prepareFlags: PrepareFlags.IsCommitted);
		}

		public static Rec TransSt(int transaction, string stream, DateTime? timestamp = null,
			byte version = PrepareLogRecord.PrepareRecordVersion) {
			return new Rec(RecType.TransStart, transaction, stream, null, timestamp, version);
		}

		public static Rec Prepare(int transaction, string stream, string eventType = null, DateTime? timestamp = null,
			long? eventNumber = null,
			byte[] data = null,
			StreamMetadata metadata = null, PrepareFlags prepareFlags = PrepareFlags.Data,
			byte version = PrepareLogRecord.PrepareRecordVersion) {
			return new Rec(RecType.Prepare, transaction, stream, eventType, timestamp, version, eventNumber, data, metadata, prepareFlags);
		}

		public static Rec Write(int transaction, string stream, string eventType = null, DateTime? timestamp = null,
			long? eventNumber = null,
			byte[] data = null,
			StreamMetadata metadata = null, PrepareFlags prepareFlags = PrepareFlags.Data | PrepareFlags.IsCommitted,
			byte version = PrepareLogRecord.PrepareRecordVersion) {
			return new Rec(RecType.Prepare, transaction, stream, eventType, timestamp, version, eventNumber, data, metadata, prepareFlags);
		}

		public static Rec TransEnd(int transaction, string stream, DateTime? timestamp = null,
			byte version = PrepareLogRecord.PrepareRecordVersion) {
			return new Rec(RecType.TransEnd, transaction, stream, null, timestamp, version);
		}

		public static Rec Commit(int transaction, string stream, DateTime? timestamp = null,
			byte version = CommitLogRecord.CommitRecordVersion) {
			return new Rec(RecType.Commit, transaction, stream, null, timestamp, version);
		}
	}
}
