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
using EventStore.Core.Data;
using EventStore.Core.Util;

namespace EventStore.Core.TransactionLog.LogRecords
{
    public static class EpochRecordExtensions
    {
        public static string AsString(this EpochRecord epoch)
        {
            return string.Format("E{0}@{1}:{2:B}",
                                 epoch == null ? -1 : epoch.EpochNumber,
                                 epoch == null ? -1 : epoch.EpochPosition,
                                 epoch == null ? Guid.Empty : epoch.EpochId);
        }

        public static string AsString(this Epoch epoch)
        {
            return string.Format("E{0}@{1}:{2:B}",
                                 epoch == null ? -1 : epoch.EpochNumber,
                                 epoch == null ? -1 : epoch.EpochPosition,
                                 epoch == null ? Guid.Empty : epoch.EpochId);
        }
    }

    public class EpochRecord
    {
        public readonly long EpochPosition;
        public readonly int EpochNumber;
        public readonly Guid EpochId;

        public readonly long PrevEpochPosition;
        public readonly DateTime TimeStamp;

        public EpochRecord(long epochPosition, int epochNumber, Guid epochId, long prevEpochPosition, DateTime timeStamp)
        {
            EpochPosition = epochPosition;
            EpochNumber = epochNumber;
            EpochId = epochId;
            PrevEpochPosition = prevEpochPosition;
            TimeStamp = timeStamp;
        }

        internal EpochRecord(EpochRecordDto dto)
                : this(dto.EpochPosition, dto.EpochNumber, dto.EpochId, dto.PrevEpochPosition, dto.TimeStamp)
        {
        }

        public byte[] AsSerialized()
        {
            return new EpochRecordDto(this).ToJsonBytes();
        }

        public override string ToString()
        {
            return string.Format("EpochPosition: {0}, EpochNumber: {1}, EpochId: {2}, PrevEpochPosition: {3}, TimeStamp: {4}, ",
                                 EpochPosition,
                                 EpochNumber,
                                 EpochId,
                                 PrevEpochPosition,
                                 TimeStamp);
        }

        internal class EpochRecordDto
        {
            public long EpochPosition{ get; set; }
            public int EpochNumber{ get; set; }
            public Guid EpochId{ get; set; }

            public long PrevEpochPosition{ get; set; }
            public DateTime TimeStamp{ get; set; }

            public EpochRecordDto()
            {
            }

            public EpochRecordDto(EpochRecord rec)
            {
                EpochPosition = rec.EpochPosition;
                EpochNumber = rec.EpochNumber;
                EpochId = rec.EpochId;

                PrevEpochPosition = rec.PrevEpochPosition;
                TimeStamp = rec.TimeStamp;
            }
        }
    }
}