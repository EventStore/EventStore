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
using System.Net.Sockets;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Transport.Tcp;

namespace EventStore.Core.Messages
{
    public static class TcpMessage
    {
        public class TcpSend: Message, IQueueAffineMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public int QueueId { get { return ConnectionManager.GetHashCode(); } }

            public readonly TcpConnectionManager ConnectionManager;
            public readonly Message Message;

            public TcpSend(TcpConnectionManager connectionManager, Message message)
            {
                ConnectionManager = connectionManager;
                Message = message;
            }
        }

        public class Heartbeat: Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly int MessageNumber;

            public Heartbeat(int messageNumber)
            {
                MessageNumber = messageNumber;
            }
        }

        public class HeartbeatTimeout: Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly int MessageNumber;

            public HeartbeatTimeout(int messageNumber)
            {
                MessageNumber = messageNumber;
            }
        }

        public class PongMessage: Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly Guid CorrelationId;
            public readonly byte[] Payload;

            public PongMessage(Guid correlationId, byte[] payload)
            {
                CorrelationId = correlationId;
                Payload = payload;
            }
        }

        public class ConnectionEstablished: Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly TcpConnectionManager Connection;

            public ConnectionEstablished(TcpConnectionManager connection)
            {
                Connection = connection;
            }
        }

        public class ConnectionClosed: Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly TcpConnectionManager Connection;
            public readonly SocketError SocketError;

            public ConnectionClosed(TcpConnectionManager connection, SocketError socketError)
            {
                Connection = connection;
                SocketError = socketError;
            }
        }

        public class NotAuthenticated : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly Guid CorrelationId;
            public readonly string Reason;

            public NotAuthenticated(Guid correlationId, string reason)
            {
                CorrelationId = correlationId;
                Reason = reason;
            }
        }

        public class Authenticated : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly Guid CorrelationId;

            public Authenticated(Guid correlationId)
            {
                CorrelationId = correlationId;
            }
        }
    }
}