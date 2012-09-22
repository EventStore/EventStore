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

namespace EventStore.Core.TransactionLog.Chunks
{
    public class TFChunkReader : ITransactionFileReader, IDisposable
    {
        private readonly TFChunkDb _db;
        private readonly ICheckpoint _checkpoint;

        public TFChunkReader(TFChunkDb db, ICheckpoint checkpoint)
        {
            Ensure.NotNull(db, "dbConfig");
            Ensure.NotNull(checkpoint, "writerCheckpoint");

            _db = db;
            _checkpoint = checkpoint;
        }

        public void Open()
        {
            // NOOP
        }

        public void Dispose()
        {
            // NOOP
        }

        public void Close()
        {
            // NOOP
        }

        public RecordReadResult TryReadAt(long position)
        {
            return TryReadAtInternal(position, 0);
        }

        private RecordReadResult TryReadAtInternal(long position, int retries)
        {
            var writerChk = _checkpoint.Read();
            if (position + 4 > writerChk)
                return new RecordReadResult(false, null, -1);

            var chunkNum = (int)(position / _db.Config.ChunkSize);
            var chunkPos = (int)(position % _db.Config.ChunkSize);
            var chunk = _db.Manager.GetChunk(chunkNum);
            if (chunk == null)
            {
                throw new InvalidOperationException(
                        string.Format(
                                      "No chunk returned for LogPosition: {0}, chunkNum: {1}, chunkPos: {2}, writer check: {3}",
                                      position,
                                      chunkNum,
                                      chunkPos,
                                      writerChk));
            }

            try
            {
                return chunk.TryReadRecordAt(chunkPos);
            }
            catch (FileBeingDeletedException)
            {
                if(retries > 100) throw new InvalidOperationException("Been told the file was deleted > 100 times. Probably a problem in db");
                return TryReadAtInternal(position, retries + 1);
            }
        }
    }
}