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
using System.Text;
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
    public class TcpConnectionManager: IHandle<TcpMessage.Heartbeat>, IHandle<TcpMessage.HeartbeatTimeout>
    {
        public const int ConnectionQueueSizeThreshold = 50000;

        private static readonly TimeSpan KeepAliveInterval = TimeSpan.FromMilliseconds(15000);
        private static readonly TimeSpan KeepAliveTimeout = TimeSpan.FromMilliseconds(60000);

        private static readonly ILogger Log = LogManager.GetLoggerFor<TcpConnectionManager>();

        public event Action<TcpConnectionManager, SocketError> ConnectionClosed;
        public event Action<TcpConnectionManager> ConnectionEstablished;

        public Guid ConnectionId { get { return _connectionId; } }
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
        private readonly Guid _connectionId;

        public TcpConnectionManager(string connectionName, 
                                    Guid connectionId,
                                    ITcpDispatcher dispatcher, 
                                    IPublisher publisher, 
                                    TcpConnection openedConnection,
                                    IPublisher networkSendQueue)
        {
            Ensure.NotEmptyGuid(connectionId, "connectionId");
            Ensure.NotNull(dispatcher, "dispatcher");
            Ensure.NotNull(publisher, "publisher");
            Ensure.NotNull(openedConnection, "openedConnnection");
           
            _connectionName = connectionName;
            _connectionId = connectionId;
            
            _tcpEnvelope = new SendOverTcpEnvelope(this, networkSendQueue);
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
                                    Guid connectionId,
                                    ITcpDispatcher dispatcher,
                                    IPublisher publisher, 
                                    IPEndPoint remoteEndPoint, 
                                    TcpClientConnector connector,
                                    IPublisher networkSendQueue)
        {
            Ensure.NotEmptyGuid(connectionId, "connectionId");
            Ensure.NotNull(dispatcher, "dispatcher");
            Ensure.NotNull(publisher, "publisher");
            Ensure.NotNull(remoteEndPoint, "remoteEndPoint");
            Ensure.NotNull(connector, "connector");

            _connectionName = connectionName;
            _connectionId = connectionId;

            _tcpEnvelope = new SendOverTcpEnvelope(this, networkSendQueue);
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
            Log.Info("Connection '{0}' ({1:B}) to [{2}] established.", _connectionName, _connectionId, connection.EffectiveEndPoint);

            ScheduleHeartbeat(0);

            var handler = ConnectionEstablished;
            if (handler != null)
                handler(this);
        }

        private void OnConnectionFailed(TcpConnection connection, SocketError socketError)
        {
            Log.Info("Connection '{0}' ({1:B}) to [{2}] failed: {3}.", _connectionName, _connectionId, connection.EffectiveEndPoint, socketError);

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

            try
            {
                _framer.UnFrameData(data);
            }
            catch (PackageFramingException exc)
            {
                SendBadRequestAndClose(Guid.Empty, string.Format("Invalid TCP frame received. Error: {0}.", exc.Message));
                return;
            }
            _connection.ReceiveAsync(OnRawDataReceived);
        }

        private void OnMessageArrived(ArraySegment<byte> data)
        {
            TcpPackage package;
            try
            {
                package = TcpPackage.FromArraySegment(data);
            }
            catch (Exception e)
            {
                SendBadRequestAndClose(Guid.Empty, string.Format("Received bad network package. Error: {0}", e));
                return;
            }

            OnPackageReceived(package);
        }

        private void OnPackageReceived(TcpPackage package)
        {
            try
            {
                switch (package.Command)
                {
                    case TcpCommand.HeartbeatResponseCommand:
                        break;
                    case TcpCommand.HeartbeatRequestCommand:
                        SendPackage(new TcpPackage(TcpCommand.HeartbeatResponseCommand, package.CorrelationId, null));
                        break;
                    case TcpCommand.BadRequest:
                    {
                        string reason = string.Empty;
                        Helper.EatException(() => reason = Encoding.UTF8.GetString(package.Data.Array, package.Data.Offset, package.Data.Count));
                        var exitMessage = string.Format("Bad request received from '{0}' [{1}, {2:B}], will stop server. CorrelationId: {3:B}, Error: {4}.",
                                                        _connectionName, EndPoint, _connectionId, package.CorrelationId,
                                                        reason.IsEmptyString() ? "<reason missing>" : reason);
                        Log.Error(exitMessage);
                        Application.Exit(1, exitMessage);
                        break;
                    }
                    default:
                    {
                        var message = _dispatcher.UnwrapPackage(package, _tcpEnvelope, this);
                        if (message != null)
                            _publisher.Publish(message);
                        else
                            SendBadRequestAndClose(package.CorrelationId, 
                                                   string.Format("Couldn't unwrap network package for command {0}.", package.Command));
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                SendBadRequestAndClose(package.CorrelationId, string.Format("Error during processing package. Error: {0}", e));
            }
        }

        public void SendBadRequestAndClose(Guid correlationId, string message)
        {
            Ensure.NotNull(message, "message");

            SendPackage(new TcpPackage(TcpCommand.BadRequest, correlationId, Encoding.UTF8.GetBytes(message)));
            Log.Error("Closing connection '{0}' [{1}, {2:B}] due to error. Reason: {3}", _connectionName, EndPoint, _connectionId, message);
            _connection.Close();
        }

        public void Stop(string reason = null)
        {
            Log.Trace("Closing connection '{0}' [{1}, {2:B}] cleanly.{3}",
                      _connectionName, EndPoint, _connectionId,
                      reason.IsEmpty() ? string.Empty : " Reason: " + reason);
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

            int queueSize;
            if ((queueSize = _connection.SendQueueSize) > ConnectionQueueSizeThreshold)
                SendBadRequestAndClose(Guid.Empty, string.Format("Connection queue size is too large: {0}.", queueSize));
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
                Log.Info("Closing connection '{0}' [{1}, {2:B}] due to HEARTBEAT TIMEOUT at msgNum {3}.", 
                         _connectionName, EndPoint, _connectionId, msgNum);
                _connection.Close();
            }
        }

        private void OnConnectionClosed(ITcpConnection connection, SocketError socketError)
        {
            Log.Info("Connection '{0}' [{1}, {2:B}] closed: {3}.", _connectionName, connection.EffectiveEndPoint, _connectionId, socketError);

            _isClosed = true;
            connection.ConnectionClosed -= OnConnectionClosed;

            var handler = ConnectionClosed;
            if (handler != null)
                handler(this, socketError);
        }

        private void ScheduleHeartbeat(int msgNum)
        {
            _publisher.Publish(TimerMessage.Schedule.Create(KeepAliveInterval,
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