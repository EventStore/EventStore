using System;
using EventStore.Common.Utils;
using EventStore.Core.Exceptions;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog.Chunks
{
    public class TFChunkSequentialReader : ITransactionFileSequentialReader
    {
        private const int MaxRetries = 100;

        public long Position { get { return _curPos; } }

        private readonly TFChunkDb _db;
        private readonly ICheckpoint _writerCheckpoint;
        private long _curPos;

        public TFChunkSequentialReader(TFChunkDb db, ICheckpoint writerCheckpoint, long initialPosition)
        {
            Ensure.NotNull(db, "dbConfig");
            Ensure.NotNull(writerCheckpoint, "writerCheckpoint");
            Ensure.Nonnegative(initialPosition, "initialPosition");

            _db = db;
            _writerCheckpoint = writerCheckpoint;
            _curPos = initialPosition;
        }


        public bool TryReadNext(out LogRecord record)
        {
            var res = TryReadNext();
            record = res.LogRecord;
            return res.Success;
        }

        public void Open()
        {
            // NOOP
        }

        public void Close()
        {
            // NOOP
        }

        public RecordReadResult TryReadNext()
        {
            return TryReadNextInternal(_curPos, 0);
        }

        private RecordReadResult TryReadNextInternal(long position, int retries)
        {
            var pos = position;
            while (true)
            {
                var writerChk = _writerCheckpoint.Read();
                if (pos >= writerChk)
                    return new RecordReadResult(false, null, -1);

                var chunkNum = (int)(pos / _db.Config.ChunkSize);
                var chunkPos = (int)(pos % _db.Config.ChunkSize);
                var chunk = _db.Manager.GetChunk(chunkNum);
                if (chunk == null)
                    throw new Exception(string.Format("No chunk returned for read next request at position: {0}, cur inner pos: {1}, chunkNum: {2}, chunkPos: {3}, writer check: {4}",
                                                      position, pos, chunkNum, chunkPos, writerChk));

                RecordReadResult result;
                try
                {
                    result = chunk.TryReadClosestForward(chunkPos);
                }
                catch (FileBeingDeletedException)
                {
                    if (retries > MaxRetries)
                        throw new Exception(string.Format("Got a file that was being deleted {0} times from TFChunkDb, likely a bug there.", MaxRetries));
                    return TryReadNextInternal(position, retries + 1);
                }

                if (result.Success)
                {
                    _curPos = chunkNum * (long)_db.Config.ChunkSize + result.NextPosition;
                    return new RecordReadResult(true, result.LogRecord, -1);
                }

                // we are the end of chunk
                pos = (chunk.ChunkHeader.ChunkEndNumber + 1) * (long)_db.Config.ChunkSize; // the start of next physical chunk
            }
        }

        public bool TryReadPrev(out LogRecord record)
        {
            var res = TryReadPrev();
            record = res.LogRecord;
            return res.Success;
        }

        public RecordReadResult TryReadPrev()
        {
            return TryReadPrevInternal(_curPos, 0);
        }

        private RecordReadResult TryReadPrevInternal(long position, int retries)
        {
            var pos = position;
            while (true)
            {
                var writerChk = _db.Config.WriterCheckpoint.Read();
                if (pos <= 0 || pos > writerChk) // we allow == writerChk, that means read the very last record
                    return new RecordReadResult(false, null, -1);

                var chunkNum = (int)(pos / _db.Config.ChunkSize);
                var chunkPos = (int)(pos % _db.Config.ChunkSize);
                var chunk = _db.Manager.GetChunk(chunkNum);
                if (chunk == null)
                    throw new Exception(string.Format("No chunk returned for read prev request at pos: {0}, cur inner pos: {1}, chunkNum: {2}, chunkPos: {3}, writer check: {4}",
                                                      position, pos, chunkNum, chunkPos, writerChk));
                bool readLast = false;
                if (chunkPos == 0 && chunk.ChunkHeader.ChunkStartNumber == chunkNum) 
                {
                    // we are exactly at the boundary of physical chunks
                    // so we switch to previous chunk and request TryReadLast
                    readLast = true;
                    chunkNum -= 1;
                    chunkPos = -1;

                    chunk = _db.Manager.GetChunk(chunkNum);
                    if (chunk == null)
                        throw new Exception(string.Format("No chunk returned for read prev request (TryReadLast case) at pos: {0}, cur inner pos: {1}, chunkNum: {2}, chunkPos: {3} , writer check: {4}", 
                                                          position, pos, chunkNum, chunkPos, writerChk));
                }

                RecordReadResult result;
                try
                {
                    result = readLast ? chunk.TryReadLast() : chunk.TryReadClosestBackwards(chunkPos);
                }
                catch (FileBeingDeletedException)
                {
                    if (retries > MaxRetries)
                        throw new Exception(string.Format("Got a file that was being deleted {0} times from TFChunkDb, likely a bug there.", MaxRetries));
                    return TryReadPrevInternal(position, retries + 1);
                }

                if (result.Success)
                {
                    _curPos = chunkNum * (long)_db.Config.ChunkSize + result.NextPosition;
                    return new RecordReadResult(true, result.LogRecord, -1);
                }

                // we are the beginning of chunk, so need to switch to previous one
                // to do that we set cur position to the exact boundary position between current and previous chunk, 
                // this will be handled correctly on next iteration
                pos = chunk.ChunkHeader.ChunkStartNumber * (long)_db.Config.ChunkSize; // the boundary of current and previous chunk
            }
        }

        public void Dispose()
        {
            Close();
        }
    }
}