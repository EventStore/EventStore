// Copyright (c) 2012, Event Store LLP
// All rights reserved.
//  
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
//  
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//  
using System;
using EventStore.Common.Utils;
using EventStore.Core.Exceptions;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog.Chunks
{
    public class TFChunkReader : ITransactionFileReader
    {
        public const int MaxRetries = 20;

        private readonly TFChunkDb _db;
        private readonly ICheckpoint _writerCheckpoint;
        private long _curPos;

        public TFChunkReader(TFChunkDb db, ICheckpoint writerCheckpoint, long initialPosition = 0)
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

        public void Reposition(long position)
        {
            _curPos = position;
        }

        public SeqReadResult TryReadNext()
        {
            return TryReadNextInternal(_curPos, 0);
        }

        private SeqReadResult TryReadNextInternal(long position, int retries)
        {
            var pos = position;
            while (true)
            {
                var writerChk = _writerCheckpoint.Read();
                if (pos >= writerChk)
                    return SeqReadResult.Failure;

                var chunk = _db.Manager.GetChunkFor(pos);
                RecordReadResult result;
                try
                {
                    result = chunk.TryReadClosestForward(chunk.ChunkHeader.GetChunkLocalLogicalPosition(pos));
                }
                catch (FileBeingDeletedException)
                {
                    if (retries > MaxRetries)
                        throw new Exception(string.Format("Got a file that was being deleted {0} times from TFChunkDb, likely a bug there.", MaxRetries));
                    return TryReadNextInternal(pos, retries + 1);
                }

                if (result.Success)
                {
                    _curPos = chunk.ChunkHeader.ChunkStartPosition + result.NextPosition;
                    var postPos = result.LogRecord.Position + result.RecordLength + 2 * sizeof(int);
                    return new SeqReadResult(true, result.LogRecord, result.RecordLength, result.LogRecord.Position, postPos);
                }

                // we are the end of chunk
                pos = chunk.ChunkHeader.ChunkEndPosition; // the start of next physical chunk
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
            return TryReadPrevInternal(_curPos, 0);
        }

        private SeqReadResult TryReadPrevInternal(long position, int retries)
        {
            var pos = position;
            while (true)
            {
                var writerChk = _writerCheckpoint.Read();
                // we allow == writerChk, that means read the very last record
                if (pos > writerChk)
                    throw new ArgumentOutOfRangeException("position", string.Format("Requested position {0} is greater than writer checkpoint {1} when requesting to read previous record from TF.", pos, writerChk));
                if (pos <= 0) 
                    return SeqReadResult.Failure;

                var chunk = _db.Manager.GetChunkFor(pos);
                bool readLast = false;
                if (pos == chunk.ChunkHeader.ChunkStartPosition) 
                {
                    // we are exactly at the boundary of physical chunks
                    // so we switch to previous chunk and request TryReadLast
                    readLast = true;
                    chunk = _db.Manager.GetChunkFor(pos - 1);
                }

                RecordReadResult result;
                try
                {
                    result = readLast ? chunk.TryReadLast() : chunk.TryReadClosestBackward(chunk.ChunkHeader.GetChunkLocalLogicalPosition(pos));
                }
                catch (FileBeingDeletedException)
                {
                    if (retries > MaxRetries)
                        throw new Exception(string.Format("Got a file that was being deleted {0} times from TFChunkDb, likely a bug there.", MaxRetries));
                    return TryReadPrevInternal(position, retries + 1);
                }

                if (result.Success)
                {
                    _curPos = chunk.ChunkHeader.ChunkStartPosition + result.NextPosition;
                    var postPos = result.LogRecord.Position + result.RecordLength + 2 * sizeof(int);
                    return new SeqReadResult(true, result.LogRecord, result.RecordLength, result.LogRecord.Position, postPos);
                }

                // we are the beginning of chunk, so need to switch to previous one
                // to do that we set cur position to the exact boundary position between current and previous chunk, 
                // this will be handled correctly on next iteration
                pos = chunk.ChunkHeader.ChunkStartPosition;
            }
        }

        public RecordReadResult TryReadAt(long position)
        {
            return TryReadAtInternal(position, 0);
        }

        private RecordReadResult TryReadAtInternal(long position, int retries)
        {
            var writerChk = _writerCheckpoint.Read();
            if (position + 2 * sizeof(int) > writerChk)
                return RecordReadResult.Failure;

            var chunk = _db.Manager.GetChunkFor(position);
            try
            {
                return chunk.TryReadAt(chunk.ChunkHeader.GetChunkLocalLogicalPosition(position));
            }
            catch (FileBeingDeletedException)
            {
                if (retries > MaxRetries)
                    throw new InvalidOperationException("Been told the file was deleted > MaxRetries times. Probably a problem in db.");
                return TryReadAtInternal(position, retries + 1);
            }
        }
    }
}