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
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Transport.Tcp;
using EventStore.Transport.Tcp.Framing;

namespace EventStore.Core.Services.Transport.Tcp
{
    /// <summary>
    /// Manager for individual TCP connection. It handles connection lifecycle,
    /// heartbeats, message framing and dispatch to the memory bus.
    /// </summary>
    public class TcpConnectionManager: IHandle<TcpMessage.Heartbeat>, 
                                       IHandle<TcpMessage.HeartbeatTimeout>
    {
        private static readonly TimeSpan KeepAliveInterval = TimeSpan.FromMilliseconds(15000);
        private static readonly TimeSpan KeepAliveTimeout = TimeSpan.FromMilliseconds(60000);

        private static readonly ILogger Log = LogManager.GetLoggerFor<TcpConnectionManager>();

        public event Action<TcpConnectionManager, SocketError> ConnectionClosed;
        public event Action<TcpConnectionManager> ConnectionEstablished;

        public string ConnectionName { get { return _connectionName; } }
        public readonly IPEndPoint EndPoint;
        public bool IsClosed { get { return _isClosed; } }
        public int SendQueueSize { get { return _connection.SendQueueSize; } }

        private readonly TcpConnection _connection;
        private readonly IEnvelope _tcpEnvelope;
        private readonly IPublisher _publisher;
        private readonly ITcpDispatcher _dispatcher;
        private readonly IMessageFramer _framer;
        
        private int _messageNumber;
        private bool _isClosed;
        private readonly string _connectionName;

        public TcpConnectionManager(string connectionName, 
                                    ITcpDispatcher dispatcher, 
                                    IPublisher publisher, 
                                    TcpConnection openedConnection)
        {
            Ensure.NotNull(dispatcher, "dispatcher");
            Ensure.NotNull(publisher, "publisher");
            Ensure.NotNull(openedConnection, "openedConnnection");
           
            _connectionName = connectionName;
            
            _tcpEnvelope = new SendOverTcpEnvelope(this);
            _publisher = publisher;
            _dispatcher = dispatcher;

            EndPoint = openedConnection.EffectiveEndPoint;

            _framer = new LengthPrefixMessageFramer();
            _framer.RegisterMessageArrivedCallback(OnMessageArrived);

            _connection = openedConnection;
            _connection.ConnectionClosed += OnConnectionClosed;
            ScheduleHeartbeat(0);
        }

        public TcpConnectionManager(string connectionName, 
                                    ITcpDispatcher dispatcher,
                                    IPublisher publisher, 
                                    IPEndPoint remoteEndPoint, 
                                    TcpClientConnector connector)
        {
            Ensure.NotNull(dispatcher, "dispatcher");
            Ensure.NotNull(publisher, "publisher");
            Ensure.NotNull(remoteEndPoint, "remoteEndPoint");
            Ensure.NotNull(connector, "connector");

            _connectionName = connectionName;

            _tcpEnvelope = new SendOverTcpEnvelope(this);
            _publisher = publisher;
            _dispatcher = dispatcher;

            EndPoint = remoteEndPoint;

            _framer = new LengthPrefixMessageFramer();
            _framer.RegisterMessageArrivedCallback(OnMessageArrived);

            _connection = connector.ConnectTo(remoteEndPoint, OnConnectionEstablished, OnConnectionFailed);
            _connection.ConnectionClosed += OnConnectionClosed;
        }

        private void OnConnectionEstablished(TcpConnection connection)
        {
            Log.Info("Connection to [{0}] established.", connection.EffectiveEndPoint);

            ScheduleHeartbeat(0);

            var handler = ConnectionEstablished;
            if (handler != null)
                handler(this);
        }

        private void OnConnectionFailed(TcpConnection connection, SocketError socketError)
        {
            Log.Info("Connection to [{0}] failed: {1}.", connection.EffectiveEndPoint, socketError);

            _isClosed = true;
            connection.ConnectionClosed -= OnConnectionClosed;

            var handler = ConnectionClosed;
            if (handler != null)
                handler(this, socketError);
        }

        public void StartReceiving()
        {
            _connection.ReceiveAsync(OnRawDataReceived);
        }

        private void OnRawDataReceived(ITcpConnection connection, IEnumerable<ArraySegment<byte>> data)
        {
            Interlocked.Increment(ref _messageNumber);

            _framer.UnFrameData(data);
            _connection.ReceiveAsync(OnRawDataReceived);
        }

        private void OnMessageArrived(ArraySegment<byte> data)
        {
            var package = TcpPackage.FromArraySegment(data);
            OnPackageReceived(package);
        }

        private void OnPackageReceived(TcpPackage package)
        {
            switch (package.Command)
            {
                case TcpCommand.HeartbeatResponseCommand:
                    break;
                case TcpCommand.HeartbeatRequestCommand:
                    SendPackage(new TcpPackage(TcpCommand.HeartbeatResponseCommand, package.CorrelationId, null));
                    break;
                default:
                {
                    var message = _dispatcher.UnwrapPackage(package, _tcpEnvelope, this);
                    if (message != null)
                        _publisher.Publish(message);
                    break;
                }
            }
        }

        public void Stop()
        {
            Log.Trace("Closing connection {0}", EndPoint);
            _connection.Close();
        }

        public void SendMessage(Message message)
        {
            var package = _dispatcher.WrapMessage(message);
            if (package != null)
                SendPackage(package.Value);
        }

        private void SendPackage(TcpPackage package)
        {
            var data = package.AsArraySegment();  
            var framed = _framer.FrameData(data);
            _connection.EnqueueSend(framed);
        }

        public void Handle(TcpMessage.Heartbeat message)
        {
            if (_isClosed)
                return;

            var msgNum = _messageNumber;
            if (message.MessageNumber != msgNum)
                ScheduleHeartbeat(msgNum);
            else
            {
                SendPackage(new TcpPackage(TcpCommand.HeartbeatRequestCommand, Guid.NewGuid(), null));

                _publisher.Publish(TimerMessage.Schedule.Create(
                    KeepAliveTimeout,
                    new SendToWeakThisEnvelope(this), 
                    new TcpMessage.HeartbeatTimeout(EndPoint, msgNum)));
            }
        }

        public void Handle(TcpMessage.HeartbeatTimeout message)
        {
            if (_isClosed)
                return;

            var msgNum = _messageNumber;
            if (message.MessageNumber != msgNum)
                ScheduleHeartbeat(msgNum);
            else 
            {
                Log.Info("[{0}] Closing due to HEARTBEAT TIMEOUT at msgNum {1}.", message.EndPoint, msgNum);
                _connection.Close();
            }
        }

        private void OnConnectionClosed(ITcpConnection connection, SocketError socketError)
        {
            Log.Info("Connection [{0}] closed: {1}.", connection.EffectiveEndPoint, socketError);

            _isClosed = true;
            connection.ConnectionClosed -= OnConnectionClosed;

            var handler = ConnectionClosed;
            if (handler != null)
                handler(this, socketError);
        }

        private void ScheduleHeartbeat(int msgNum)
        {
            _publisher.Publish(TimerMessage.Schedule.Create(
                KeepAliveInterval,
                new SendToWeakThisEnvelope(this), 
                new TcpMessage.Heartbeat(EndPoint, msgNum)));
        }

        private class SendToWeakThisEnvelope : IEnvelope
        {
            private readonly WeakReference _receiver;

            public SendToWeakThisEnvelope(object receiver)
            {
                _receiver = new WeakReference(receiver);
            }

            public void ReplyWith<T>(T message) where T : Message
            {
                var x = _receiver.Target as IHandle<T>;
                if (x != null)
                    x.Handle(message);
            }
        }
    }
}