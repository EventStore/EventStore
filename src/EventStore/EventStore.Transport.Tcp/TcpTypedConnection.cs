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
using EventStore.Common.Log;
using EventStore.Transport.Tcp.Formatting;
using EventStore.Transport.Tcp.Framing;

namespace EventStore.Transport.Tcp
{
    public class TcpTypedConnection<T>
    {
        private static readonly ILogger Log = LogManager.GetLogger("TcpTypedConnection");

        public event Action<TcpTypedConnection<T>, SocketError> ConnectionClosed;

        private readonly ITcpConnection _connection;
        private readonly IMessageFormatter<T> _formatter;
        private readonly IMessageFramer _framer;

        private Action<TcpTypedConnection<T>, T> _receiveCallback;

        public IPEndPoint EffectiveEndPoint { get; private set; }

        public int SendQueueSize
        {
            get { return _connection.SendQueueSize; }
        }

        public TcpTypedConnection(ITcpConnection connection,
                                  IMessageFormatter<T> formatter,
                                  IMessageFramer framer)
        {
            if (formatter == null)
                throw new ArgumentNullException("formatter");
            if (framer == null)
                throw new ArgumentNullException("framer");

            _connection = connection;
            _formatter = formatter;
            _framer = framer;
            EffectiveEndPoint = connection.EffectiveEndPoint;

            connection.ConnectionClosed += OnConnectionClosed;

            //Setup callback for incoming messages
            framer.RegisterMessageArrivedCallback(IncomingMessageArrived);
        }

        private void OnConnectionClosed(ITcpConnection connection, SocketError socketError)
        {
            connection.ConnectionClosed -= OnConnectionClosed;

            var handler = ConnectionClosed;
            if (handler != null)
                handler(this, socketError);
        }

        public void EnqueueSend(T message)
        {
            var data = _formatter.ToArraySegment(message);
            _connection.EnqueueSend(_framer.FrameData(data));
        }

        public void ReceiveAsync(Action<TcpTypedConnection<T>, T> callback)
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
            try
            {
                _framer.UnFrameData(data);
            }
            catch (PackageFramingException exc)
            {
                Log.InfoException(exc, "Invalid TCP frame received.");
                Close("Invalid TCP frame received.");
                return;
            }
            connection.ReceiveAsync(OnRawDataReceived);
        }

        private void IncomingMessageArrived(ArraySegment<byte> message)
        {
            _receiveCallback(this, _formatter.From(message));
        }

        public void Close(string reason = null)
        {
            _connection.Close(reason);
        }
    }
}