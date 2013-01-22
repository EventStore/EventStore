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
using EventStore.Core.Util;

namespace EventStore.Core.TransactionLog.LogRecords
{
    public enum SystemRecordType: byte
    {
        Invalid = 0,
        Epoch = 1
    }

    public enum SystemRecordSerialization: byte
    {
        Invalid = 0,
        Binary = 1,
        Json = 2,
        Bson = 3
    }

    public class SystemLogRecord: LogRecord, IEquatable<SystemLogRecord>
    {
        public const byte SystemRecordVersion = 0;

        public override long Position { get { return LogPosition; } }

        public readonly long LogPosition;
        public readonly DateTime TimeStamp;
        public readonly SystemRecordType SystemRecordType;
        public readonly SystemRecordSerialization SystemRecordSerialization;
        public readonly long Reserved;
        public readonly byte[] Data;

        public SystemLogRecord(long logPosition,
                               DateTime timeStamp,
                               SystemRecordType systemRecordType,
                               SystemRecordSerialization systemRecordSerialization,
                               byte[] data)
            : base(LogRecordType.System, SystemRecordVersion)
        {
            Ensure.Nonnegative(logPosition, "logPosition");

            LogPosition = logPosition;
            TimeStamp = timeStamp;
            SystemRecordType = systemRecordType;
            SystemRecordSerialization = systemRecordSerialization;
            Reserved = 0;
            Data = data ?? NoData;
        }

        internal SystemLogRecord(BinaryReader reader, byte version): base(LogRecordType.System, version)
        {
            LogPosition = reader.ReadInt64();
            TimeStamp = new DateTime(reader.ReadInt64());
            SystemRecordType = (SystemRecordType) reader.ReadByte();
            if (SystemRecordType == SystemRecordType.Invalid)
                throw new ArgumentException(string.Format("Invalid SystemRecordType {0} at LogPosition {1}.", SystemRecordType, LogPosition));
            SystemRecordSerialization = (SystemRecordSerialization) reader.ReadByte();
            if (SystemRecordSerialization == SystemRecordSerialization.Invalid)
                throw new ArgumentException(string.Format("Invalid SystemRecordSerialization {0} at LogPosition {1}.", SystemRecordSerialization, LogPosition));
            Reserved = reader.ReadInt64();

            var dataCount = reader.ReadInt32();
            Data = dataCount == 0 ? NoData : reader.ReadBytes(dataCount);
        }

        public EpochRecord GetEpochRecord()
        {
            if (SystemRecordType != SystemRecordType.Epoch)
            {
                throw new ArgumentException(string.Format("Unexpected type of system record. Requested: {0}, actual: {1}.",
                                                          SystemRecordType.Epoch,
                                                          SystemRecordType), 
                                            "SystemRecordType");
            }

            switch (SystemRecordSerialization)
            {
                case SystemRecordSerialization.Json:
                {
                    var dto = Data.ParseJson<EpochRecord.EpochRecordDto>();
                    return new EpochRecord(dto);
                }
                default:
                    throw new ArgumentOutOfRangeException(
                            string.Format("Unexpected SystemRecordSerialization type: {0}", SystemRecordSerialization),
                            "SystemRecordSerialization");
            }
        }

        public override void WriteTo(BinaryWriter writer)
        {
            base.WriteTo(writer);

            writer.Write(LogPosition);
            writer.Write(TimeStamp.Ticks);
            writer.Write((byte)SystemRecordType);
            writer.Write((byte)SystemRecordSerialization);
            writer.Write(Reserved);
            writer.Write(Data.Length);
            writer.Write(Data);
        }

        public bool Equals(SystemLogRecord other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return other.LogPosition == LogPosition
                   && other.TimeStamp.Equals(TimeStamp)
                   && other.SystemRecordType == SystemRecordType
                   && other.SystemRecordSerialization == SystemRecordSerialization
                   && other.Reserved == Reserved;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (SystemRecordType)) return false;
            return Equals((SystemLogRecord) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = LogPosition.GetHashCode();
                result = (result * 397) ^ TimeStamp.GetHashCode();
                result = (result * 397) ^ SystemRecordType.GetHashCode();
                result = (result * 397) ^ SystemRecordSerialization.GetHashCode();
                result = (result * 397) ^ Reserved.GetHashCode();
                return result;
            }
        }

        public static bool operator ==(SystemLogRecord left, SystemLogRecord right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(SystemLogRecord left, SystemLogRecord right)
        {
            return !Equals(left, right);
        }

        public override string ToString()
        {
            return string.Format("LogPosition: {0}, "
                                 + "TimeStamp: {1}, "
                                 + "SystemRecordType: {2}, "
                                 + "SystemRecordSerialization: {3}, "
                                 + "Reserved: {4}",
                                 LogPosition,
                                 TimeStamp,
                                 SystemRecordType,
                                 SystemRecordSerialization,
                                 Reserved);
        }
    }
}