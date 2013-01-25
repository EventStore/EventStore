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
using EventStore.Core.Util;

namespace EventStore.Core.TransactionLog.LogRecords
{
    public class EpochRecord
    {
        public readonly long LogPosition;
        public readonly DateTime TimeStamp;

        public readonly int EpochNumber;
        public readonly Guid EpochId;

        public readonly long PrevEpochPosition;

        public EpochRecord(long logPosition, DateTime timeStamp, int epochNumber, Guid epochId, long prevEpochPosition)
        {
            LogPosition = logPosition;
            TimeStamp = timeStamp;
            EpochNumber = epochNumber;
            EpochId = epochId;
            PrevEpochPosition = prevEpochPosition;
        }

        internal EpochRecord(EpochRecordDto dto)
                : this(dto.LogPosition, dto.TimeStamp, dto.EpochNumber, dto.EpochId, dto.PrevEpochPosition)
        {
        }

        public byte[] AsSerialized()
        {
            return new EpochRecordDto(this).ToJsonBytes();
        }

        internal class EpochRecordDto
        {
            public long LogPosition{ get; set; }
            public DateTime TimeStamp{ get; set; }

            public int EpochNumber{ get; set; }
            public Guid EpochId{ get; set; }

            public long PrevEpochPosition{ get; set; }

            public EpochRecordDto()
            {
            }

            public EpochRecordDto(EpochRecord rec)
            {
                LogPosition = rec.LogPosition;
                TimeStamp = rec.TimeStamp;

                EpochNumber = rec.EpochNumber;
                EpochId = rec.EpochId;

                PrevEpochPosition = rec.PrevEpochPosition;
            }
        }
    }
}