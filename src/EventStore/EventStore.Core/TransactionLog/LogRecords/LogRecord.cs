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
using EventStore.Core.Data;
using EventStore.Core.Services;

namespace EventStore.Core.TransactionLog.LogRecords
{
    public abstract class LogRecord
    {
        public static readonly byte[] NoData = new byte[0];

        public readonly LogRecordType RecordType;
        public readonly byte Version;

        public abstract long Position { get; }

        public static LogRecord ReadFrom(BinaryReader reader)
        {
            var recordType = (LogRecordType)reader.ReadByte();
            var version = reader.ReadByte();

            switch (recordType)
            {
                case LogRecordType.Prepare:
                    return new PrepareLogRecord(reader, version);
                case LogRecordType.Commit:
                    return new CommitLogRecord(reader, version);
                default:
                    throw new ArgumentOutOfRangeException("recordType");
            }
        }

        public static PrepareLogRecord Prepare(long logPosition, Guid correlationId, Guid eventId, long transactionPos, int transactionOffset,
                                               string eventStreamId, int expectedVersion, PrepareFlags flags, string eventType, 
                                               byte[] data, byte[] metadata, DateTime? timeStamp = null)
        {
            return new PrepareLogRecord(logPosition, correlationId, eventId, transactionPos, transactionOffset, 
                                        eventStreamId, expectedVersion, timeStamp ?? DateTime.UtcNow, flags, eventType,
                                        data, metadata);
        }

        public static CommitLogRecord Commit(long logPosition, Guid correlationId, long startPosition, int eventNumber)
        {
            return new CommitLogRecord(logPosition, correlationId, startPosition, DateTime.UtcNow, eventNumber);
        }

        public static PrepareLogRecord SingleWrite(long logPosition, Guid correlationId, Guid eventId, string eventStreamId, 
                                                   int expectedVersion, string eventType, byte[] data, byte[] metadata, 
                                                   DateTime? timestamp = null)
        {
            return new PrepareLogRecord(logPosition, correlationId, eventId, logPosition, 0, eventStreamId, expectedVersion, 
                                        timestamp ?? DateTime.UtcNow, 
                                        PrepareFlags.Data | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd, 
                                        eventType, data, metadata);
        }

        public static PrepareLogRecord TransactionBegin(long logPos, Guid correlationId, string eventStreamId, int expectedVersion)
        {
            return new PrepareLogRecord(logPos, correlationId, Guid.NewGuid(), logPos, -1, eventStreamId, expectedVersion, 
                                        DateTime.UtcNow, PrepareFlags.TransactionBegin, null, NoData, NoData);
        }

        public static PrepareLogRecord TransactionWrite(long logPosition, Guid correlationId, Guid eventId, long transactionPos, int transactionOffset, string eventStreamId, string eventType, byte[] data, byte[] metadata, bool isJson)
        {
            return new PrepareLogRecord(logPosition, correlationId, eventId, transactionPos, transactionOffset,
                                        eventStreamId, ExpectedVersion.Any, DateTime.UtcNow, PrepareFlags.Data | (isJson ? PrepareFlags.IsJson : PrepareFlags.None), 
                                        eventType, data, metadata);
        }

        public static PrepareLogRecord TransactionEnd(long logPos, Guid correlationId, Guid eventId, long transactionPos, string eventStreamId)
        {
            return new PrepareLogRecord(logPos, correlationId, eventId, transactionPos, -1, eventStreamId, ExpectedVersion.Any, 
                                        DateTime.UtcNow, PrepareFlags.TransactionEnd, null, NoData, NoData);
        }

        public static PrepareLogRecord DeleteTombstone(long logPosition, Guid correlationId, string eventStreamId, int expectedVersion)
        {
            return new PrepareLogRecord(logPosition, correlationId, Guid.NewGuid(), logPosition, 0, eventStreamId, 
                                        expectedVersion, DateTime.UtcNow, 
                                        PrepareFlags.StreamDelete | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd, 
                                        SystemEventTypes.StreamDeleted, NoData, NoData);
        }

        public static PrepareLogRecord StreamCreated(long logPosition, Guid correlationId, long transactionPos, 
                                                     string eventStreamId, byte[] metadata, bool isImplicit, DateTime? timestamp = null)
        {
            return new PrepareLogRecord(logPosition, correlationId, Guid.NewGuid(), transactionPos, 0, eventStreamId, 
                                        ExpectedVersion.NoStream, timestamp ?? DateTime.UtcNow, 
                                        PrepareFlags.Data | PrepareFlags.TransactionBegin, 
                                        isImplicit ? SystemEventTypes.StreamCreatedImplicit : SystemEventTypes.StreamCreated, 
                                        NoData, metadata);
        }

        protected LogRecord(LogRecordType recordType, byte version)
        {
            RecordType = recordType;
            Version = version;
        }

        public virtual void WriteTo(BinaryWriter writer)
        {
            writer.Write((byte) RecordType);
            writer.Write(Version);
        }

        public int GetSizeWithLengthPrefixAndSuffix()
        {
            using (var memoryStream = new MemoryStream())
            {
                WriteTo(new BinaryWriter(memoryStream));
                return 8 + (int)memoryStream.Length;
            }
        }

        internal void WriteWithLengthPrefixAndSuffixTo(BinaryWriter writer)
        {
            using (var memoryStream = new MemoryStream())
            {
                WriteTo(new BinaryWriter(memoryStream));
                var length = (int) memoryStream.Length;
                writer.Write(length);
                writer.Write(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
                writer.Write(length);
            }
        }
    }
}