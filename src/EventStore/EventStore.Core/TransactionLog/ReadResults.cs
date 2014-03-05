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
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog
{
    public struct RecordReadResult
    {
        public static readonly RecordReadResult Failure = new RecordReadResult(false, -1, null, 0);

        public readonly bool Success;
        public readonly long NextPosition;
        public readonly LogRecord LogRecord;
        public readonly int RecordLength;

        public RecordReadResult(bool success, long nextPosition, LogRecord logRecord, int recordLength)
        {
            Success = success;
            LogRecord = logRecord;
            NextPosition = nextPosition;
            RecordLength = recordLength;
        }

        public override string ToString()
        {
            return string.Format("Success: {0}, NextPosition: {1}, RecordLength: {2}, LogRecord: {3}",
                                 Success,
                                 NextPosition,
                                 RecordLength,
                                 LogRecord);
        }
    }

    public struct SeqReadResult
    {
        public static readonly SeqReadResult Failure = new SeqReadResult(false, true, null, 0, -1, -1);

        public readonly bool Success;
        public readonly bool Eof;
        public readonly LogRecord LogRecord;
        public readonly int RecordLength;
        public readonly long RecordPrePosition;
        public readonly long RecordPostPosition;

        public SeqReadResult(bool success, 
                             bool eof,
                             LogRecord logRecord, 
                             int recordLength, 
                             long recordPrePosition, 
                             long recordPostPosition)
        {
            Success = success;
            Eof = eof;
            LogRecord = logRecord;
            RecordLength = recordLength;
            RecordPrePosition = recordPrePosition;
            RecordPostPosition = recordPostPosition;
        }

        public override string ToString()
        {
            return string.Format("Success: {0}, RecordLength: {1}, RecordPrePosition: {2}, RecordPostPosition: {3}, LogRecord: {4}",
                                 Success,
                                 RecordLength,
                                 RecordPrePosition,
                                 RecordPostPosition,
                                 LogRecord);
        }
    }
}