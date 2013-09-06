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

using System.Collections.Generic;
using System.Security.Principal;
using EventStore.Core.Data;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Services.Storage.ReaderIndex
{
    public interface IReadIndex
    {
        long LastCommitPosition { get; }
        IIndexWriter IndexWriter { get; }

        void Init(long buildToPosition);
        void Commit(CommitLogRecord record);
        void Commit(IList<PrepareLogRecord> commitedPrepares);
        ReadIndexStats GetStatistics();
        
        IndexReadEventResult ReadEvent(string streamId, int eventNumber);
        IndexReadStreamResult ReadStreamEventsBackward(string streamId, int fromEventNumber, int maxCount);
        IndexReadStreamResult ReadStreamEventsForward(string streamId, int fromEventNumber, int maxCount);
        /// <summary>
        /// Returns event records in the sequence they were committed into TF.
        /// Positions is specified as pre-positions (pointer at the beginning of the record).
        /// </summary>
        IndexReadAllResult ReadAllEventsForward(TFPos pos, int maxCount);
        /// <summary>
        /// Returns event records in the reverse sequence they were committed into TF.
        /// Positions is specified as post-positions (pointer after the end of record).
        /// </summary>
        IndexReadAllResult ReadAllEventsBackward(TFPos pos, int maxCount);

        StreamInfo GetStreamInfo(string streamId);
        bool IsStreamDeleted(string streamId);
        int GetLastStreamEventNumber(string streamId);
        StreamMetadata GetStreamMetadata(string streamId);

        string GetEventStreamIdByTransactionId(long transactionId);
        StreamAccess CheckStreamAccess(string streamId, StreamAccessType streamAccessType, IPrincipal user);

        void Close();
        void Dispose();
    }
}