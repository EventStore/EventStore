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
using System.Net.Sockets;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Transport.Tcp;

namespace EventStore.Core.Messages
{
    public static class TcpMessage
    {
        public class TcpSend: Message, IQueueAffineMessage
        {
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
            public readonly IPEndPoint EndPoint;
            public readonly int MessageNumber;

            public Heartbeat(IPEndPoint endPoint, int messageNumber)
            {
                if (endPoint == null) 
                    throw new ArgumentNullException("endPoint");
                EndPoint = endPoint;
                MessageNumber = messageNumber;
            }
        }

        public class HeartbeatTimeout: Message
        {
            public readonly IPEndPoint EndPoint;
            public readonly int MessageNumber;

            public HeartbeatTimeout(IPEndPoint endPoint, int messageNumber)
            {
                if (endPoint == null) 
                    throw new ArgumentNullException("endPoint");
                EndPoint = endPoint;
                MessageNumber = messageNumber;
            }
        }

        public class PongMessage: Message
        {
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
            public readonly TcpConnectionManager Connection;

            public ConnectionEstablished(TcpConnectionManager connection)
            {
                Connection = connection;
            }
        }

        public class ConnectionClosed: Message
        {
            public readonly TcpConnectionManager Connection;
            public readonly SocketError SocketError;

            public ConnectionClosed(TcpConnectionManager connection, SocketError socketError)
            {
                Connection = connection;
                SocketError = socketError;
            }
        }
    }
}