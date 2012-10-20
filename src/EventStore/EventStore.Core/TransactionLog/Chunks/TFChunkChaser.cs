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
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Exceptions;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog.Chunks
{
    public class TFChunkChaser : ITransactionFileChaser
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<TFChunkChaser>();

        public long Position { get { return _curPos; } }

        private readonly TFChunkDb _db;
        private readonly ICheckpoint _writerCheckpoint;
        private readonly ICheckpoint _chaserCheckpoint;
        private long _curPos;

        public TFChunkChaser(TFChunkDb db, ICheckpoint writerCheckpoint, ICheckpoint chaserCheckpoint)
        {
            Ensure.NotNull(db, "dbConfig");
            Ensure.NotNull(writerCheckpoint, "writerCheckpoint");
            Ensure.NotNull(chaserCheckpoint, "chaserCheckpoint");

            _db = db;
            _writerCheckpoint = writerCheckpoint;
            _chaserCheckpoint = chaserCheckpoint;
            _curPos = _chaserCheckpoint.Read();
        }

        public void Open()
        {
            // NOOP
        }

        public bool TryReadNext(out LogRecord record)
        {
            var res = TryReadNext();
            record = res.LogRecord;
            return res.Success;
        }

        public RecordReadResult TryReadNext()
        {
            return TryReadNext(_curPos, 0);
        }

        private RecordReadResult TryReadNext(long position, int trial)
        {
            return TryReadNextInternal(position, trial, 0);
        }

        private RecordReadResult TryReadNextInternal(long position, int trial, int retries)
        {
            var writerChk = _writerCheckpoint.Read();
            if (position >= writerChk)
                return new RecordReadResult(false, null, -1);

            var chunkNum = (int)(position / _db.Config.ChunkSize);
            var chunkPos = (int)(position % _db.Config.ChunkSize);
            var chunk = _db.Manager.GetChunk(chunkNum);
            if (chunk == null)
            {
                if (trial < 3)
                {
                    Log.Fatal("RECEIVED NULL CHUNK!!! Position: {0}, WriterChk: {1}, ChunkNum: {2}, ChunkPos: {3}. TRIAL: {4}. Trying one more time...", position, writerChk, chunkNum, chunkPos, trial);
                    return TryReadNext(position, trial + 1);
                }
                throw new Exception(string.Format("No chunk returned for LogPosition: {0}, chunkNum: {1}, chunkPos: {2}, writer check: {3}", _curPos, chunkNum, chunkPos, writerChk));
            }

            RecordReadResult result; 
            try
            {
                result = position == 0 ? chunk.TryReadFirst() : chunk.TryReadSameOrClosest(chunkPos);
            }
            catch(FileBeingDeletedException)
            {
                if(retries > 100) throw new Exception("Got a file that was being deleted 100 times from chunkdb, likely a bug there.");
                return TryReadNextInternal(position, trial, retries + 1);
            }

            if (result.Success)
            {
                _curPos = chunkNum * (long)_db.Config.ChunkSize + result.NextPosition;
            }
            else
            {
                // we are the end of chunk
                chunkNum += 1;
                chunk = _db.Manager.GetChunk(chunkNum);
                if (chunk == null)
                {
                    if (trial < 3)
                    {
                        Log.Fatal("RECEIVED NULL CHUNK #{0} ON TRYING TO READ FIRST AFTER PREVIOUS CHUNK WAS FULL!!! Position: {1}, WriterChk: {2}. TRIAL: {3}. Trying one more time...", chunkNum, position, writerChk, trial);
                        return TryReadNext(position, trial + 1);
                    }
                    throw new InvalidOperationException(string.Format("No chunk returned for LogPosition: {0}, chunkNum: {1}, chunkPos: {2}, writer check: {3}", _curPos, chunkNum, chunkPos, writerChk));
                }
                try
                {
                    result = chunk.TryReadFirst();
                }
                catch(FileBeingDeletedException)
                {
                    TryReadNextInternal(position, trial, retries + 1);
                }
                if (!result.Success)
                {
                    result = chunk.TryReadFirst();
                    throw new Exception(string.Format("The record should be at the beginning of chunk #{0} but was not there. Pos: {1}.", chunkNum + 1, position));
                }
                _curPos = chunkNum * (long)_db.Config.ChunkSize + result.NextPosition;
            }

            _chaserCheckpoint.Write(_curPos);
            return new RecordReadResult(true, result.LogRecord, -1);

        }


        public void Dispose()
        {
            Close();
        }

        public void Close()
        {
            Flush();
        }

        public void Flush()
        {
            _chaserCheckpoint.Flush();
        }
    }
}