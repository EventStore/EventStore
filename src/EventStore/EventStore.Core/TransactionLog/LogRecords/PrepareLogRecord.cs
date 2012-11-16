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
using System.Linq;
using EventStore.Common.Utils;

namespace EventStore.Core.TransactionLog.LogRecords
{
    [Flags]
    public enum PrepareFlags: ushort
    {
        None = 0x00,
        Data = 0x01,                      // prepare contains data
        TransactionBegin = 0x02,          // prepare starts transaction
        TransactionEnd = 0x04,            // prepare ends transaction
        StreamDelete = 0x08,              // prepare deletes stream

        //IsCommited = 0x20,              // prepare should be considered committed immediately, no commit will follow in TF
        //Update = 0x30,                  // prepare updates previous instance of the same event, DANGEROUS!

        IsJson = 0x100,                   // indicates data & metadata are valid json

        // aggregate flag set
        DeleteTombstone = TransactionBegin | TransactionEnd | StreamDelete,
        SingleWrite = Data | TransactionBegin | TransactionEnd
    }

    public class PrepareLogRecord: LogRecord, IEquatable<PrepareLogRecord>
    {
        public const byte PrepareRecordVersion = 0;

        public readonly long LogPosition;
        public readonly PrepareFlags Flags;
        public readonly long TransactionPosition;
        public readonly int TransactionOffset;
        public readonly int ExpectedVersion;
        public readonly string EventStreamId;

        public readonly Guid EventId;
        public readonly Guid CorrelationId;
        public readonly DateTime TimeStamp;
        public readonly string EventType;
        public readonly byte[] Data;
        public readonly byte[] Metadata;

        public override long Position
        {
            get { return LogPosition; }
        }

        public long InMemorySize
        {
            get
            {
                return sizeof(LogRecordType)
                       + 1
                       + 8
                       + sizeof(PrepareFlags)
                       + 8
                       + 4
                       + 4
                       + IntPtr.Size + EventStreamId.Length * 2

                       + 16
                       + 16
                       + 8
                       + IntPtr.Size + EventType.Length*2
                       + IntPtr.Size + Data.Length
                       + IntPtr.Size + Metadata.Length;
            }
        }

        public PrepareLogRecord(long logPosition, 
                                Guid correlationId, 
                                Guid eventId,
                                long transactionPosition,
                                int transactionOffset,
                                string eventStreamId,
                                int expectedVersion,
                                DateTime timeStamp, 
                                PrepareFlags flags,
                                string eventType, 
                                byte[] data,
                                byte[] metadata)
            : base(LogRecordType.Prepare, PrepareRecordVersion)
        {
            Ensure.Nonnegative(logPosition, "logPosition");
            Ensure.NotEmptyGuid(correlationId, "correlationId");
            Ensure.NotEmptyGuid(eventId, "eventId");
            Ensure.Nonnegative(transactionPosition, "transactionPosition");
            if (transactionOffset < -1)
                throw new ArgumentOutOfRangeException("transactionOffset");
            Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
            if (expectedVersion < Core.Data.ExpectedVersion.Any)
                throw new ArgumentOutOfRangeException("expectedVersion");
            Ensure.NotNull(data, "data");

            LogPosition = logPosition;
            Flags = flags;
            TransactionPosition = transactionPosition;
            TransactionOffset = transactionOffset;
            ExpectedVersion = expectedVersion;
            EventStreamId = eventStreamId;
            
            EventId = eventId;
            CorrelationId = correlationId;
            TimeStamp = timeStamp;
            EventType = eventType ?? string.Empty;
            Data = data;
            Metadata = metadata ?? NoData;
        }

        internal PrepareLogRecord(BinaryReader reader, byte version): base(LogRecordType.Prepare, version)
        {
            LogPosition = reader.ReadInt64();
            Flags = (PrepareFlags) reader.ReadUInt16();
            TransactionPosition = reader.ReadInt64();
            TransactionOffset = reader.ReadInt32();
            ExpectedVersion = reader.ReadInt32();
            EventStreamId = reader.ReadString();
            EventId = new Guid(reader.ReadBytes(16));
            CorrelationId = new Guid(reader.ReadBytes(16));
            TimeStamp = new DateTime(reader.ReadInt64());
            EventType = reader.ReadString();

            var dataCount = reader.ReadInt32();
            Data = dataCount == 0 ? NoData : reader.ReadBytes(dataCount);
            
            var metadataCount = reader.ReadInt32();
            Metadata = metadataCount == 0 ? NoData : reader.ReadBytes(metadataCount);
        }

        public override void WriteTo(BinaryWriter writer)
        {
            base.WriteTo(writer);

            writer.Write(LogPosition);
            writer.Write((ushort) Flags);
            writer.Write(TransactionPosition);
            writer.Write(TransactionOffset);
            writer.Write(ExpectedVersion);
            writer.Write(EventStreamId);

            writer.Write(EventId.ToByteArray());
            writer.Write(CorrelationId.ToByteArray());
            writer.Write(TimeStamp.Ticks);
            writer.Write(EventType);
            writer.Write(Data.Length);
            writer.Write(Data);
            writer.Write(Metadata.Length);
            writer.Write(Metadata);
        }

        public bool Equals(PrepareLogRecord other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return other.LogPosition == LogPosition
                   && other.Flags == Flags
                   && other.TransactionPosition == TransactionPosition
                   && other.TransactionOffset == TransactionOffset
                   && other.ExpectedVersion == ExpectedVersion
                   && other.EventStreamId.Equals(EventStreamId)

                   && other.EventId == EventId
                   && other.CorrelationId == CorrelationId
                   && other.TimeStamp.Equals(TimeStamp)
                   && other.EventType.Equals(EventType)
                   && other.Data.SequenceEqual(Data)
                   && other.Metadata.SequenceEqual(Metadata);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof(PrepareLogRecord)) return false;
            return Equals((PrepareLogRecord)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = LogPosition.GetHashCode();
                result = (result * 397) ^ Flags.GetHashCode();
                result = (result * 397) ^ TransactionPosition.GetHashCode();
                result = (result * 397) ^ TransactionOffset;
                result = (result * 397) ^ ExpectedVersion;
                result = (result * 397) ^ EventStreamId.GetHashCode();

                result = (result * 397) ^ EventId.GetHashCode();
                result = (result * 397) ^ CorrelationId.GetHashCode();
                result = (result * 397) ^ TimeStamp.GetHashCode();
                result = (result * 397) ^ EventType.GetHashCode();
                result = (result * 397) ^ Data.GetHashCode();
                result = (result * 397) ^ Metadata.GetHashCode();
                return result;
            }
        }

        public static bool operator ==(PrepareLogRecord left, PrepareLogRecord right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(PrepareLogRecord left, PrepareLogRecord right)
        {
            return !Equals(left, right);
        }

        public override string ToString()
        {
            return string.Format("LogPosition: {0}, "
                                 + "Flags: {1}, "
                                 + "TransactionPosition: {2}, "
                                 + "TransactionOffset: {3}, "
                                 + "ExpectedVersion: {4}, "
                                 + "EventStreamId: {5}, "
                                 + "EventId: {6}, "
                                 + "CorrelationId: {7}, " 
                                 + "TimeStamp: {8}, " 
                                 + "EventType: {9}, " 
                                 + "InMemorySize: {10}",
                                 LogPosition,
                                 Flags,
                                 TransactionPosition,
                                 TransactionOffset,
                                 ExpectedVersion,
                                 EventStreamId,
                                 EventId,
                                 CorrelationId,
                                 TimeStamp,
                                 EventType,
                                 InMemorySize);
        }
    }
}