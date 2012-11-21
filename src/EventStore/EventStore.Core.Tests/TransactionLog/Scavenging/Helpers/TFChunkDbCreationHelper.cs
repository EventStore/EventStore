using System;
using System.Collections.Generic;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Services;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Tests.TransactionLog.Scavenging.Helpers
{
    public class TFChunkDbCreationHelper
    {
        private readonly TFChunkDbConfig _dbConfig;
        private readonly TFChunkDb _db;

        private readonly List<Rec[]> _chunkRecs = new List<Rec[]>();

        private bool _completeLast;

        public TFChunkDbCreationHelper(TFChunkDbConfig dbConfig)
        {
            Ensure.NotNull(dbConfig, "dbConfig");
            _dbConfig = dbConfig;

            _db = new TFChunkDb(_dbConfig);
            _db.OpenVerifyAndClean();

            if (_db.Config.WriterCheckpoint.ReadNonFlushed() > 0)
                throw new Exception("The DB already contains some data.");
        }

        public TFChunkDbCreationHelper Chunk(params Rec[] records)
        {
            _chunkRecs.Add(records);
            return this;
        }

        public TFChunkDbCreationHelper CompleteLastChunk()
        {
            _completeLast = true;
            return this;
        }

        public DbResult CreateDb()
        {
            var records = new LogRecord[_chunkRecs.Count][];
            for (int i = 0; i < records.Length; ++i)
            {
                records[i] = new LogRecord[_chunkRecs[i].Length];
            }

            var transactions = new Dictionary<int, TransactionInfo>();
            var streams = new Dictionary<string, int>();

            for (int i = 0; i < _chunkRecs.Count; ++i)
            {
                for (int j = 0; j < _chunkRecs[i].Length; ++j)
                {
                    var rec = _chunkRecs[i][j];

                    TransactionInfo transInfo;
                    bool transCreate = transactions.TryGetValue(rec.Transaction, out transInfo);
                    if (!transCreate)
                    {
                        if (rec.Type == Rec.RecType.Commit)
                            throw new Exception("Commit for non-existing transaction.");

                        transactions[rec.Transaction] = transInfo = new TransactionInfo(rec.StreamId, rec.Id, rec.Id);

                        streams[rec.StreamId] = -1;
                    }
                    else
                    {
                        if (rec.Type == Rec.RecType.TransStart)
                            throw new Exception(string.Format("Not expected record type: {0}.", rec.Type));
                    }

                    if (transInfo.StreamId != rec.StreamId)
                    {
                        throw new Exception(string.Format("Wrong stream id for transaction. Transaction StreamId: {0}, record StreamId: {1}.",
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

            for (int i = 0; i < _chunkRecs.Count; ++i)
            {
                var chunk = i == 0 ? _db.Manager.GetChunk(0) : _db.Manager.AddNewChunk();
                _db.Config.WriterCheckpoint.Write(i * (long)_db.Config.ChunkSize);

                for (int j = 0; j < _chunkRecs[i].Length; ++j)
                {
                    var rec = _chunkRecs[i][j];
                    var transInfo = transactions[rec.Transaction];
                    var logPos = _db.Config.WriterCheckpoint.ReadNonFlushed();

                    int curStreamVersion = streams[rec.StreamId];
                    if (curStreamVersion == -1 && rec.Type != Rec.RecType.TransStart && rec.Type != Rec.RecType.Create)
                        throw new Exception(string.Format("Stream {0} is empty and we are not creating it with stream created.", rec.StreamId));
                    if (curStreamVersion == EventNumber.DeletedStream && rec.Type != Rec.RecType.Commit)
                        throw new Exception(string.Format("Stream {0} was deleted, but we need to write some more prepares.", rec.StreamId));

                    if (transInfo.FirstPrepareId == rec.Id)
                    {
                        transInfo.TransactionPosition = logPos;
                        transInfo.TransactionEventNumber = curStreamVersion + 1;
                        transInfo.TransactionOffset = 0;
                    }

                    LogRecord record;

                    var expectedVersion = transInfo.FirstPrepareId == rec.Id ? curStreamVersion : ExpectedVersion.Any;
                    switch (rec.Type)
                    {
                        case Rec.RecType.Prepare:
                        case Rec.RecType.Create:
                        {
                            int transOffset = transInfo.TransactionOffset;
                            transInfo.TransactionOffset += 1;

                            record = LogRecord.Prepare(logPos, 
                                                       Guid.NewGuid(), 
                                                       rec.Id, 
                                                       transInfo.TransactionPosition,
                                                       transOffset,
                                                       rec.StreamId,
                                                       expectedVersion,
                                                       PrepareFlags.Data
                                                       | (transInfo.FirstPrepareId == rec.Id ? PrepareFlags.TransactionBegin : PrepareFlags.None)
                                                       | (transInfo.LastPrepareId == rec.Id ? PrepareFlags.TransactionEnd : PrepareFlags.None), 
                                                       rec.EventType,
                                                       rec.Id.ToByteArray(),
                                                       null,
                                                       rec.TimeStamp);
                            
                            streams[transInfo.StreamId] += 1;
                            break;
                        }

                        case Rec.RecType.Delete:
                        {
                            int transOffset = transInfo.TransactionOffset;
                            transInfo.TransactionOffset += 1;

                            record = LogRecord.Prepare(logPos, 
                                                       Guid.NewGuid(), 
                                                       rec.Id, 
                                                       transInfo.TransactionPosition,
                                                       transOffset,
                                                       rec.StreamId,
                                                       expectedVersion,
                                                       PrepareFlags.StreamDelete
                                                       | (transInfo.FirstPrepareId == rec.Id ? PrepareFlags.TransactionBegin : PrepareFlags.None)
                                                       | (transInfo.LastPrepareId == rec.Id ? PrepareFlags.TransactionEnd : PrepareFlags.None), 
                                                       rec.EventType,
                                                       LogRecord.NoData,
                                                       null,
                                                       rec.TimeStamp);
                            break;
                        }

                        case Rec.RecType.TransStart:
                        case Rec.RecType.TransEnd:
                        {
                            record = LogRecord.Prepare(logPos,
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
                            break;
                        }
                        case Rec.RecType.Commit:
                        {
                            record = LogRecord.Commit(logPos, Guid.NewGuid(), transInfo.TransactionPosition, transInfo.TransactionEventNumber);

                            if (transInfo.IsDelete)
                                streams[rec.StreamId] = EventNumber.DeletedStream;
                            else
                                streams[rec.StreamId] = transInfo.TransactionEventNumber + transInfo.TransactionOffset - 1;
                            break;
                        }
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    var writerRes = chunk.TryAppend(record);
                    if (!writerRes.Success)
                        throw new Exception(string.Format("Couldn't write log record: {0}", record));
                    _db.Config.WriterCheckpoint.Write(i * (long)_db.Config.ChunkSize + writerRes.NewPosition);

                    records[i][j] = record;
                }

                if (i < _chunkRecs.Count-1 || (_completeLast && i == _chunkRecs.Count - 1))
                    chunk.Complete();
            }

            return new DbResult(_db, records, streams);
        }
    }

    public class TransactionInfo
    {
        public readonly string StreamId;
        public readonly Guid FirstPrepareId;
        public Guid LastPrepareId;
        public long TransactionPosition = -1;
        public int TransactionOffset = 0;
        public int TransactionEventNumber = -1;
        public bool IsDelete = false;

        public TransactionInfo(string streamId, Guid firstPrepareId, Guid lastPrepareId)
        {
            StreamId = streamId;
            FirstPrepareId = firstPrepareId;
            LastPrepareId = lastPrepareId;
        }
    }

    public class DbResult
    {
        public readonly TFChunkDb Db;
        public readonly LogRecord[][] Recs;
        public readonly Dictionary<string, int> Streams;

        public DbResult(TFChunkDb db, LogRecord[][] recs, Dictionary<string, int> streams)
        {
            Ensure.NotNull(db, "db");
            Ensure.NotNull(recs, "recs");
            Ensure.NotNull(streams, "streams");

            Db = db;
            Recs = recs;
            Streams = streams;
        }
    }

    public class Rec
    {
        public enum RecType { Prepare, Delete, Create, TransStart, TransEnd, Commit }

        public readonly RecType Type;
        public readonly Guid Id;
        public readonly int Transaction;
        public readonly string StreamId;
        public readonly string EventType;
        public readonly DateTime TimeStamp;

        public Rec(RecType type, int transaction, string streamId, string eventType, DateTime? timestamp)
        {
            Ensure.NotNullOrEmpty(streamId, "streamId");
            Ensure.Nonnegative(transaction, "transaction");

            Type = type;
            Id = Guid.NewGuid();
            Transaction = transaction;
            StreamId = streamId;
            EventType = eventType ?? string.Empty;
            TimeStamp = timestamp ?? DateTime.UtcNow;
        }

        public static Rec Delete(int transaction, string stream, DateTime? timestamp = null)
        {
            return new Rec(RecType.Delete, transaction, stream, SystemEventTypes.StreamDeleted, timestamp);
        }

        public static Rec Create(int transaction, string stream, bool isImplicit = false, DateTime? timestamp = null)
        {
            return new Rec(RecType.Create,
                           transaction,
                           stream,
                           isImplicit ? SystemEventTypes.StreamCreatedImplicit : SystemEventTypes.StreamCreated,
                           timestamp);
        }

        public static Rec TransSt(int transaction, string stream, DateTime? timestamp = null)
        {
            return new Rec(RecType.TransStart, transaction, stream, null, timestamp);
        }

        public static Rec Prepare(int transaction, string stream, string eventType = null, DateTime? timestamp = null)
        {
            return new Rec(RecType.Prepare, transaction, stream, eventType, timestamp);
        }

        public static Rec TransEnd(int transaction, string stream, DateTime? timestamp = null)
        {
            return new Rec(RecType.TransEnd, transaction, stream, null, timestamp);
        }

        public static Rec Commit(int transaction, string stream, DateTime? timestamp = null)
        {
            return new Rec(RecType.Commit, transaction, stream, null, timestamp);
        }
    }
}