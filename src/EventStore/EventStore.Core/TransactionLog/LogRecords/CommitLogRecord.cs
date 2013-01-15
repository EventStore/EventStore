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
using System.IO;
using EventStore.Common.Utils;

namespace EventStore.Core.TransactionLog.LogRecords
{
    public class CommitLogRecord: LogRecord, IEquatable<CommitLogRecord>
    {
        public const byte CommitRecordVersion = 0;

        public readonly long LogPosition;
        public readonly long TransactionPosition;
        public readonly int FirstEventNumber;
        public readonly long SortKey;
        public readonly Guid CorrelationId;
        public readonly DateTime TimeStamp;

        public override long Position
        {
            get { return LogPosition; }
        }

        public CommitLogRecord(long logPosition,
                               Guid correlationId,
                               long transactionPosition,
                               DateTime timeStamp,
                               int firstEventNumber)
            : base(LogRecordType.Commit, CommitRecordVersion)
        {
            Ensure.Nonnegative(logPosition, "logPosition");
            Ensure.NotEmptyGuid(correlationId, "correlationId");
            Ensure.Nonnegative(transactionPosition, "TransactionPosition");
            Ensure.Nonnegative(firstEventNumber, "eventNumber");

            LogPosition = logPosition;
            TransactionPosition = transactionPosition;
            FirstEventNumber = firstEventNumber;
            SortKey = logPosition;
            CorrelationId = correlationId;
            TimeStamp = timeStamp;
        }

        internal CommitLogRecord(BinaryReader reader, byte version): base(LogRecordType.Commit, version)
        {
            LogPosition = reader.ReadInt64();
            TransactionPosition = reader.ReadInt64();
            FirstEventNumber = reader.ReadInt32();
            SortKey = reader.ReadInt64();
            CorrelationId = new Guid(reader.ReadBytes(16));
            TimeStamp = new DateTime(reader.ReadInt64());
        }

        public override void WriteTo(BinaryWriter writer)
        {
            base.WriteTo(writer);

            writer.Write(LogPosition);
            writer.Write(TransactionPosition);
            writer.Write(FirstEventNumber);
            writer.Write(SortKey);
            writer.Write(CorrelationId.ToByteArray());
            writer.Write(TimeStamp.Ticks);
        }

        public bool Equals(CommitLogRecord other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return other.LogPosition == LogPosition
                   && other.TransactionPosition == TransactionPosition
                   && other.FirstEventNumber == FirstEventNumber
                   && other.SortKey == SortKey
                   && other.CorrelationId == CorrelationId
                   && other.TimeStamp.Equals(TimeStamp);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (CommitLogRecord)) return false;
            return Equals((CommitLogRecord) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = LogPosition.GetHashCode();
                result = (result * 397) ^ TransactionPosition.GetHashCode();
                result = (result * 397) ^ FirstEventNumber.GetHashCode();
                result = (result * 397) ^ SortKey.GetHashCode();
                result = (result * 397) ^ CorrelationId.GetHashCode();
                result = (result * 397) ^ TimeStamp.GetHashCode();
                return result;
            }
        }

        public static bool operator ==(CommitLogRecord left, CommitLogRecord right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(CommitLogRecord left, CommitLogRecord right)
        {
            return !Equals(left, right);
        }

        public override string ToString()
        {
            return string.Format("LogPosition: {0}, "
                                 + "TransactionPosition: {1}, "
                                 + "FirstEventNumber: {2}, "
                                 + "SortKey: {3}, "
                                 + "CorrelationId: {4}, " 
                                 + "TimeStamp: {5}",
                                 LogPosition,
                                 TransactionPosition,
                                 FirstEventNumber,
                                 SortKey,
                                 CorrelationId,
                                 TimeStamp);
        }
    }
}