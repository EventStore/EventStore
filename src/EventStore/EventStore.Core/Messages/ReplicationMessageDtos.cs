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

namespace EventStore.Core.Messages
{
    public static class ReplicationMessageDto
    {
        [ProtoContract]
        public class PrepareAck
        {
            [ProtoMember(1)] 
            public byte[] CorrelationId { get; set; }

            [ProtoMember(2)] 
            public long LogPosition { get; set; }

            [ProtoMember(3)]
            public byte Flags { get; set; }

            public PrepareAck()
            {
            }

            public PrepareAck(Guid correlationId, long logPosition, byte flags)
            {
                CorrelationId = correlationId.ToByteArray();
                LogPosition = logPosition;
                Flags = flags;
            }
        }

        [ProtoContract]
        public class CommitAck
        {
            [ProtoMember(1)]
            public byte[] CorrelationId { get; set; }

            [ProtoMember(2)]
            public long StartPosition { get; set; }

            [ProtoMember(3)]
            public int EventNumber { get; set; }

            public CommitAck()
            {
            }

            public CommitAck(Guid correlationId, long startPosition, int eventVersion)
            {
                CorrelationId = correlationId.ToByteArray();
                StartPosition = startPosition;
                EventNumber = eventVersion;
            }
        }

        [ProtoContract]
        public class SubscribeReplica
        {
            [ProtoMember(1)]
            public long LogPosition { get; set; }

            [ProtoMember(2)]
            public byte[] Ip { get; set; }

            [ProtoMember(3)]
            public int Port { get; set; }

            [ProtoMember(4)]
            public bool  IsPromotable { get; set; }

            public SubscribeReplica()
            {
            }

            public SubscribeReplica(long logPosition, byte[] ip, int port, bool isPromotable)
            {
                LogPosition = logPosition;
                Ip = ip;
                Port = port;
                IsPromotable = isPromotable;
            }
        }

        [ProtoContract]
        public class LogBulk
        {
            [ProtoMember(1)]
            public long LogPosition { get; set; }

            [ProtoMember(2)]
            public byte[] LogData { get; set; }

            [ProtoMember(3)]
            public long MasterWriterChecksum { get; set; }

            public LogBulk()
            {
            }

            public LogBulk(long logPosition, byte[] logData, long masterWriterChecksum)
            {
                LogPosition = logPosition;
                LogData = logData;
                MasterWriterChecksum = masterWriterChecksum;
            }
        }

        [ProtoContract]
        public class SlaveAssignment
        {
            [ProtoMember(1)]
            public byte[] Ip { get; set; }

            [ProtoMember(2)]
            public int Port { get; set; }

            public SlaveAssignment()
            {
            }

            public SlaveAssignment(byte[] ip, int port)
            {
                Ip = ip;
                Port = port;
            }
        }

        [ProtoContract]
        public class CloneAssignment
        {
            [ProtoMember(1)]
            public byte[] Ip { get; set; }

            [ProtoMember(2)]
            public int Port { get; set; }

            public CloneAssignment()
            {
            }

            public CloneAssignment(byte[] ip, int port)
            {
                Ip = ip;
                Port = port;
            }
        }
    }
}
