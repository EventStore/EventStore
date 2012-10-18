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

        public void Reposition(long position)
        {
            _curPos = position;
        }

        public SeqReadResult TryReadNext()
        {
            return TryReadNextInternal(_curPos, 0, allowNonFlushed: false);
        }

        public SeqReadResult TryReadNextNonFlushed()
        {
            return TryReadNextInternal(_curPos, 0, allowNonFlushed: true);
        }

        private SeqReadResult TryReadNextInternal(long position, int retries, bool allowNonFlushed)
        {
            var pos = position;
            while (true)
            {
                var writerChk = allowNonFlushed ? _writerCheckpoint.ReadNonFlushed() : _writerCheckpoint.Read();
                if (pos >= writerChk)
                    return SeqReadResult.Failure;

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
                    return TryReadNextInternal(position, retries + 1, allowNonFlushed);
                }

                if (result.Success)
                {
                    _curPos = chunkNum * (long)_db.Config.ChunkSize + result.NextPosition;
                    var postPos = result.LogRecord.Position + result.RecordLength + 2 * sizeof(int);
                    return new SeqReadResult(true, result.LogRecord, result.RecordLength, result.LogRecord.Position, postPos);
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

        public SeqReadResult TryReadPrev()
        {
            return TryReadPrevInternal(_curPos, 0, allowNonFlushed: false);
        }

        public SeqReadResult TryReadPrevNonFlushed()
        {
            return TryReadPrevInternal(_curPos, 0, allowNonFlushed: true);
        }

        private SeqReadResult TryReadPrevInternal(long position, int retries, bool allowNonFlushed)
        {
            var pos = position;
            while (true)
            {
                var writerChk = allowNonFlushed ? _writerCheckpoint.ReadNonFlushed() : _writerCheckpoint.Read();
                // we allow == writerChk, that means read the very last record
                if (pos > writerChk)
                    throw new ArgumentOutOfRangeException("position", string.Format("Requested position {0} is greater than writer checkpoint {1} when requesting to read previous record from TF.", pos, writerChk));
                if (pos <= 0) 
                    return SeqReadResult.Failure;

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
                    result = readLast ? chunk.TryReadLast() : chunk.TryReadClosestBackward(chunkPos);
                }
                catch (FileBeingDeletedException)
                {
                    if (retries > MaxRetries)
                        throw new Exception(string.Format("Got a file that was being deleted {0} times from TFChunkDb, likely a bug there.", MaxRetries));
                    return TryReadPrevInternal(position, retries + 1, allowNonFlushed);
                }

                if (result.Success)
                {
                    _curPos = chunkNum * (long)_db.Config.ChunkSize + result.NextPosition;
                    var postPos = result.LogRecord.Position + result.RecordLength + 2 * sizeof(int);
                    return new SeqReadResult(true, result.LogRecord, result.RecordLength, result.LogRecord.Position, postPos);
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