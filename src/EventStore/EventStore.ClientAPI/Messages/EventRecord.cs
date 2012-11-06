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
using ProtoBuf;

namespace EventStore.ClientAPI.Messages
{
    [ProtoContract]
    internal class EventRecord
    {
        [ProtoMember(1)]
        public readonly int EventNumber;

        [ProtoMember(2)]
        public readonly long LogPosition;

        [ProtoMember(3)]
        public readonly byte[] CorrelationId;

        [ProtoMember(4)]
        public readonly byte[] EventId;

        [ProtoMember(5)]
        public readonly long TransactionPosition;

        [ProtoMember(6)]
        public readonly int TransactionOffset;

        [ProtoMember(7)]
        public readonly string EventStreamId;

        [ProtoMember(8)]
        public readonly int ExpectedVersion;

        [ProtoMember(9)]
        public readonly DateTime TimeStamp;

        [ProtoMember(10)]
        public readonly ushort Flags;

        [ProtoMember(11)]
        public readonly string EventType;

        [ProtoMember(12)]
        public readonly byte[] Data;

        [ProtoMember(13)]
        public readonly byte[] Metadata;

        public EventRecord()
        {
        }

        public EventRecord(int eventNumber, 
                           long logPosition, 
                           byte[] correlationId, 
                           byte[] eventId, 
                           long transactionPosition, 
                           int transactionOffset, 
                           string eventStreamId, 
                           int expectedVersion, 
                           DateTime timeStamp, 
                           ushort flags, 
                           string eventType, 
                           byte[] data, 
                           byte[] metadata)
        {
            EventNumber = eventNumber;
            LogPosition = logPosition;
            CorrelationId = correlationId;
            EventId = eventId;
            TransactionPosition = transactionPosition;
            TransactionOffset = transactionOffset;
            EventStreamId = eventStreamId;
            ExpectedVersion = expectedVersion;
            TimeStamp = timeStamp;
            Flags = flags;
            EventType = eventType;
            Data = data;
            Metadata = metadata;
        }
    }
}