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

namespace EventStore.Core.Services.Storage.ReaderIndex
{
    public struct IndexReadStreamResult
    {
        internal static readonly EventRecord[] EmptyRecords = new EventRecord[0];

        public readonly int FromEventNumber;
        public readonly int MaxCount;

        public readonly ReadStreamResult Result;
        public readonly int NextEventNumber;
        public readonly int LastEventNumber;
        public readonly bool IsEndOfStream;

        public readonly EventRecord[] Records;
        public readonly StreamMetadata Metadata;

        public IndexReadStreamResult(int fromEventNumber, int maxCount, ReadStreamResult result)
        {
            if (result == ReadStreamResult.Success)
                throw new ArgumentException(String.Format("Wrong ReadStreamResult provided for failure constructor: {0}.", result), "result");

            FromEventNumber = fromEventNumber;
            MaxCount = maxCount;

            Result = result;
            NextEventNumber = -1;
            LastEventNumber = -1;
            IsEndOfStream = true;
            Records = EmptyRecords;
            Metadata = null;
        }

        public IndexReadStreamResult(int fromEventNumber, 
                                     int maxCount, 
                                     EventRecord[] records, 
                                     StreamMetadata metadata,
                                     int nextEventNumber, 
                                     int lastEventNumber, 
                                     bool isEndOfStream)
        {
            Ensure.NotNull(records, "records");

            FromEventNumber = fromEventNumber;
            MaxCount = maxCount;

            Result = ReadStreamResult.Success;
            Records = records;
            Metadata = metadata;
            NextEventNumber = nextEventNumber;
            LastEventNumber = lastEventNumber;
            IsEndOfStream = isEndOfStream;
        }

        public override string ToString()
        {
            return String.Format("FromEventNumber: {0}, Maxcount: {1}, Result: {2}, Record count: {3}, Metadata: {4}, " 
                                 + "NextEventNumber: {5}, LastEventNumber: {6}, IsEndOfStream: {7}",
                                 FromEventNumber,
                                 MaxCount,
                                 Result,
                                 Records.Length,
                                 Metadata,
                                 NextEventNumber,
                                 LastEventNumber,
                                 IsEndOfStream);
        }
    }
}