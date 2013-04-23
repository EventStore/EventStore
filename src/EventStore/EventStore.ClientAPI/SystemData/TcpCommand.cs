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
namespace EventStore.ClientAPI.SystemData
{
    public enum TcpCommand: byte
    {
        HeartbeatRequestCommand = 0x01,
        HeartbeatResponseCommand = 0x02,

        Ping = 0x03,
        Pong = 0x04,

        PrepareAck = 0x05,
        CommitAck = 0x06,

        SlaveAssignment = 0x07,
        CloneAssignment = 0x08,

        SubscribeReplica = 0x10,
        ReplicaLogPositionAck = 0x11,
        CreateChunk = 0x12,
        RawChunkBulk = 0x13,
        DataChunkBulk = 0x14,
        ReplicaSubscriptionRetry = 0x15,
        ReplicaSubscribed = 0x16,

        // CLIENT COMMANDS
//        CreateStream = 0x80,
//        CreateStreamCompleted = 0x81,

        WriteEvents = 0x82,
        WriteEventsCompleted = 0x83,

        TransactionStart = 0x84,
        TransactionStartCompleted = 0x85,
        TransactionWrite = 0x86,
        TransactionWriteCompleted = 0x87,
        TransactionCommit = 0x88,
        TransactionCommitCompleted = 0x89,

        DeleteStream = 0x8A,
        DeleteStreamCompleted = 0x8B,

        ReadEvent = 0xB0,
        ReadEventCompleted = 0xB1,
        ReadStreamEventsForward = 0xB2,
        ReadStreamEventsForwardCompleted = 0xB3,
        ReadStreamEventsBackward = 0xB4,
        ReadStreamEventsBackwardCompleted = 0xB5,
        ReadAllEventsForward = 0xB6,
        ReadAllEventsForwardCompleted = 0xB7,
        ReadAllEventsBackward = 0xB8,
        ReadAllEventsBackwardCompleted = 0xB9,

        SubscribeToStream = 0xC0,
        SubscriptionConfirmation = 0xC1,
        StreamEventAppeared = 0xC2,
        UnsubscribeFromStream = 0xC3,
        SubscriptionDropped = 0xC4,

        ScavengeDatabase = 0xD0,

        BadRequest = 0xF0,
        NotHandled = 0xF1
    }
}