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
using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Tests.TransactionLog.Chunks
{
    internal class FakeReadIndex: IReadIndex
    {
        public long LastCommitPosition { get { throw new NotImplementedException(); } }

        private readonly Func<string, bool> _isStreamDeleted;

        public FakeReadIndex(Func<string, bool> isStreamDeleted)
        {
            Ensure.NotNull(isStreamDeleted, "isStreamDeleted");
            _isStreamDeleted = isStreamDeleted;
        }

        public void Commit(CommitLogRecord record)
        {
            throw new NotImplementedException();
        }

        public PrepareLogRecord ReadPrepare(long pos)
        {
            throw new NotImplementedException();
        }

        public SingleReadResult ReadEvent(string streamId, int eventNumber, out EventRecord record)
        {
            throw new NotImplementedException();
        }

        public RangeReadResult ReadStreamEventsBackward(string streamId, int fromEventNumber, int maxCount, out EventRecord[] records)
        {
            throw new NotImplementedException();
        }

        public RangeReadResult ReadStreamEventsForward(string streamId, int fromEventNumber, int maxCount, out EventRecord[] records)
        {
            throw new NotImplementedException();
        }

        public int GetLastStreamEventNumber(string streamId)
        {
            throw new NotImplementedException();
        }

        public bool IsStreamDeleted(string streamId)
        {
            return _isStreamDeleted(streamId);
        }

        public IndexReadAllResult ReadAllEventsBackward(TFPos pos, int maxCount)
        {
            throw new NotImplementedException();
        }

        public EventRecord ResolveLinkToEvent(EventRecord eventRecord)
        {
            throw new NotImplementedException();
        }

        public CommitCheckResult CheckCommitStartingAt(long prepareStartPosition)
        {
            throw new NotImplementedException();
        }

        public IndexReadAllResult ReadAllEventsForward(TFPos pos, int maxCount)
        {
            throw new NotImplementedException();
        }

        public int GetLastTransactionOffset(long writerCheckpoint, long transactionId)
        {
            throw new NotImplementedException();
        }

        public void Build()
        {
            throw new NotImplementedException();
        }

        public ReadIndexStats GetStatistics()
        {
            throw new NotImplementedException();
        }

        public void Close()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
