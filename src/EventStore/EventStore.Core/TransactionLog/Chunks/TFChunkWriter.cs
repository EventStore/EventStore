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
using System.Diagnostics;
using EventStore.Common.Utils;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog.Chunks
{
    public class TFChunkWriter: ITransactionFileWriter
    {
        public ICheckpoint Checkpoint { get { return _writerCheckpoint; } }
        
        private readonly TFChunkDb _db;
        private readonly ICheckpoint _writerCheckpoint;

        private long _writerPos;
        private TFChunk _writerChunk;
 
        public TFChunkWriter(TFChunkDb db)
        {
            Ensure.NotNull(db, "db");

            _db = db;
            _writerCheckpoint = db.Config.WriterCheckpoint;
            _writerPos = _writerCheckpoint.Read();
            _writerChunk = db.Manager.GetChunkFor(_writerPos);
            if (_writerChunk == null)
                throw new InvalidOperationException("No chunk given for existing position.");
        }

        public void Open()
        {
            // DO NOTHING
        }

        public bool Write(LogRecord record, out long newPos)
        {
            var chunkNum = (int)(_writerPos / _db.Config.ChunkSize);
            var chunkPos = _writerPos % _db.Config.ChunkSize;

            var result = _writerChunk.TryAppend(record);
            if (result.Success)
            {
                Debug.Assert(result.OldPosition == chunkPos);
                _writerPos = chunkNum * (long)_db.Config.ChunkSize + result.NewPosition;
            }
            else
            {
                _writerChunk.Flush();
                _writerChunk.Complete();
                _writerChunk = _db.Manager.AddNewChunk();
                //_writerCheckpoint.Flush(); //flush our checkpoint
                _writerPos = (_writerChunk.ChunkHeader.ChunkStartNumber) * (long)_db.Config.ChunkSize; // we just moved to a new chunk at pos 0
                //GFY CANT USE chunkNum here (it could be exact at end)
            }
            _writerCheckpoint.Write(_writerPos);
            newPos = _writerPos;
            return result.Success;
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
            _writerChunk.Flush();
            _writerCheckpoint.Flush();
        }
    }
}
