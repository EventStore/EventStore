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
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using EventStore.ClientAPI.Tcp;

namespace EventStore.ClientAPI.Transport.Tcp
{
    class TcpTypedConnection
    {
        public event Action<TcpTypedConnection, SocketError> ConnectionClosed;

        private readonly TcpConnection _connection;
        private readonly LengthPrefixMessageFramer _framer;

        private Action<TcpTypedConnection, byte[]> _receiveCallback;

        public IPEndPoint EffectiveEndPoint { get; private set; }

        public int SendQueueSize
        {
            get { return _connection.SendQueueSize; }
        }

        public TcpTypedConnection(TcpConnection connection)
        {
            _connection = connection;
            _framer = new LengthPrefixMessageFramer();
            EffectiveEndPoint = connection.EffectiveEndPoint;

            connection.ConnectionClosed += OnConnectionClosed;

            //Setup callback for incoming messages
            _framer.RegisterMessageArrivedCallback(IncomingMessageArrived);
        }

        private void OnConnectionClosed(ITcpConnection connection, SocketError socketError)
        {
            connection.ConnectionClosed -= OnConnectionClosed;

            var handler = ConnectionClosed;
            if (handler != null)
                handler(this, socketError);
        }

        public void EnqueueSend(byte[] message)
        {
            var data = new ArraySegment<byte>(message);
            _connection.EnqueueSend(_framer.FrameData(data));
        }

        public void ReceiveAsync(Action<TcpTypedConnection, byte[]> callback)
        {
            if (_receiveCallback != null)
                throw new InvalidOperationException("ReceiveAsync should be called just once.");

            if (callback == null)
                throw new ArgumentNullException("callback");

            _receiveCallback = callback;

            _connection.ReceiveAsync(OnRawDataReceived);
        }

        private void OnRawDataReceived(ITcpConnection connection, IEnumerable<ArraySegment<byte>> data)
        {
            _framer.UnFrameData(data);
            connection.ReceiveAsync(OnRawDataReceived);
        }

        private void IncomingMessageArrived(ArraySegment<byte> message)
        {
            _receiveCallback(this, message.Array);
        }

        public void Close()
        {
            _connection.Close();
        }
    }
}