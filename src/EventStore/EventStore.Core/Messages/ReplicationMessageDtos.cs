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
using System.Net;
using EventStore.Common.Utils;
using EventStore.Core.TransactionLog.Chunks;
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
        public class CreateChunk
        {
            [ProtoMember(1)]
            public byte[] MasterIp { get; set; }

            [ProtoMember(2)]
            public int MasterPort { get; set; }

            [ProtoMember(3)]
            public byte[] ChunkHeaderBytes { get; set; }

            [ProtoMember(4)]
            public int FileSize { get; set; }

            [ProtoMember(5)]
            public bool IsCompletedChunk { get; set; }

            public CreateChunk()
            {
            }

            public CreateChunk(IPEndPoint masterEndPoint, byte[] chunkHeaderBytes, int fileSize, bool isCompletedChunk)
            {
                Ensure.NotNull(masterEndPoint, "masterEndPoint");
                Ensure.NotNull(chunkHeaderBytes, "chunkHeaderBytes");

                MasterIp = masterEndPoint.Address.GetAddressBytes();
                MasterPort = masterEndPoint.Port;

                ChunkHeaderBytes = chunkHeaderBytes;

                FileSize = fileSize;
                IsCompletedChunk = isCompletedChunk;
            }
        }

        [ProtoContract]
        public class PhysicalChunkBulk
        {
            [ProtoMember(1)]
            public byte[] MasterIp { get; set; }

            [ProtoMember(2)]
            public int MasterPort { get; set; }

            [ProtoMember(3)]
            public int ChunkStartNumber { get; set; }

            [ProtoMember(4)]
            public int ChunkEndNumber { get; set; }

            [ProtoMember(5)]
            public int PhysicalPosition { get; set; }

            [ProtoMember(6)]
            public byte[] RawBytes { get; set; }

            [ProtoMember(7)]
            public bool CompleteChunk { get; set; }

            public PhysicalChunkBulk()
            {
            }

            public PhysicalChunkBulk(IPEndPoint masterEndPoint,
                                     int chunkStartNumber,
                                     int chunkEndNumber,
                                     int physicalPosition,
                                     byte[] rawBytes,
                                     bool completeChunk)
            {
                Ensure.NotNull(masterEndPoint, "masterEndPoint");
                Ensure.NotNull(rawBytes, "rawBytes");
                Ensure.Positive(rawBytes.Length, "rawBytes.Length"); // we should never send empty array, NEVER

                MasterIp = masterEndPoint.Address.GetAddressBytes();
                MasterPort = masterEndPoint.Port;

                ChunkStartNumber = chunkStartNumber;
                ChunkEndNumber = chunkEndNumber;
                PhysicalPosition = physicalPosition;
                RawBytes = rawBytes;
                CompleteChunk = completeChunk;
            }
        }

        [ProtoContract]
        public class LogicalChunkBulk
        {
            [ProtoMember(1)]
            public byte[] MasterIp { get; set; }

            [ProtoMember(2)]
            public int MasterPort { get; set; }

            [ProtoMember(3)]
            public int ChunkStartNumber { get; set; }

            [ProtoMember(4)]
            public int ChunkEndNumber { get; set; }

            [ProtoMember(5)]
            public int LogicalPosition { get; set; }

            [ProtoMember(6)]
            public byte[] DataBytes { get; set; }

            [ProtoMember(7)]
            public bool CompleteChunk { get; set; }

            public LogicalChunkBulk(IPEndPoint masterEndPoint,
                                    int chunkStartNumber,
                                    int chunkEndNumber,
                                    int logicalPosition,
                                    byte[] dataBytes,
                                    bool completeChunk)
            {
                Ensure.NotNull(masterEndPoint, "masterEndPoint");
                Ensure.NotNull(dataBytes, "rawBytes");
                Ensure.Nonnegative(dataBytes.Length, "dataBytes.Length"); // we CAN send empty dataBytes array here, unlike as with completed chunks

                MasterIp = masterEndPoint.Address.GetAddressBytes();
                MasterPort = masterEndPoint.Port;

                ChunkStartNumber = chunkStartNumber;
                ChunkEndNumber = chunkEndNumber;
                LogicalPosition = logicalPosition;
                DataBytes = dataBytes;
                CompleteChunk = completeChunk;
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
