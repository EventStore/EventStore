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

using EventStore.Common.Utils;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog.Chunks
{
    public class TFChunkChaser : ITransactionFileChaser
    {
        public ICheckpoint Checkpoint { get { return _chaserCheckpoint; } }

        private readonly ICheckpoint _chaserCheckpoint;
        private readonly TFChunkReader _reader;

        public TFChunkChaser(TFChunkDb db, ICheckpoint writerCheckpoint, ICheckpoint chaserCheckpoint)
        {
            Ensure.NotNull(db, "dbConfig");
            Ensure.NotNull(writerCheckpoint, "writerCheckpoint");
            Ensure.NotNull(chaserCheckpoint, "chaserCheckpoint");

            _chaserCheckpoint = chaserCheckpoint;
            _reader = new TFChunkReader(db, writerCheckpoint, _chaserCheckpoint.Read());
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

        public SeqReadResult TryReadNext()
        {
            var res = _reader.TryReadNext();
            if (res.Success)
                _chaserCheckpoint.Write(res.RecordPostPosition);
            else
                _chaserCheckpoint.Write(_reader.CurrentPosition);
            return res;
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