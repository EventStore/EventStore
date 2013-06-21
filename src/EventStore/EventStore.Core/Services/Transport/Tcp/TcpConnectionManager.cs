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
using System.Security.Principal;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Services.Transport.Http.Authentication;
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

        private static readonly ILogger Log = LogManager.GetLoggerFor<TcpConnectionManager>();

        public readonly Guid ConnectionId;
        public readonly string ConnectionName;
        public IPEndPoint RemoteEndPoint { get { return _connection.RemoteEndPoint; } }
        public IPEndPoint LocalEndPoint { get { return _connection.LocalEndPoint; } }
        public bool IsClosed { get { return _isClosed != 0; } }
        public int SendQueueSize { get { return _connection.SendQueueSize; } }

        private readonly ITcpConnection _connection;
        private readonly IEnvelope _tcpEnvelope;
        private readonly IPublisher _publisher;
        private readonly ITcpDispatcher _dispatcher;
        private readonly IMessageFramer _framer;
        private int _messageNumber;
        private int _isClosed;

        private readonly Action<TcpConnectionManager, SocketError> _connectionClosed;
        private readonly Action<TcpConnectionManager> _connectionEstablished;

        private readonly TimeSpan _heartbeatInterval;
        private readonly TimeSpan _heartbeatTimeout;

        private readonly InternalAuthenticationProvider _authProvider;
        private UserCredentials _defaultUser;

        public TcpConnectionManager(string connectionName, 
                                    ITcpDispatcher dispatcher, 
                                    IPublisher publisher, 
                                    ITcpConnection openedConnection,
                                    IPublisher networkSendQueue,
                                    InternalAuthenticationProvider authProvider,
                                    TimeSpan heartbeatInterval,
                                    TimeSpan heartbeatTimeout,
                                    Action<TcpConnectionManager, SocketError> onConnectionClosed)
        {
            Ensure.NotNull(dispatcher, "dispatcher");
            Ensure.NotNull(publisher, "publisher");
            Ensure.NotNull(openedConnection, "openedConnnection");
            Ensure.NotNull(networkSendQueue, "networkSendQueue");
            Ensure.NotNull(authProvider, "authProvider");

            ConnectionId = openedConnection.ConnectionId;
            ConnectionName = connectionName;

            _tcpEnvelope = new SendOverTcpEnvelope(this, networkSendQueue);
            _publisher = publisher;
            _dispatcher = dispatcher;
            _authProvider = authProvider;

            _framer = new LengthPrefixMessageFramer();
            _framer.RegisterMessageArrivedCallback(OnMessageArrived);

            _heartbeatInterval = heartbeatInterval;
            _heartbeatTimeout = heartbeatTimeout;

            _connectionClosed = onConnectionClosed;

            _connection = openedConnection;
            _connection.ConnectionClosed += OnConnectionClosed;
            if (_connection.IsClosed)
            {
                OnConnectionClosed(_connection, SocketError.Success);
                return;
            }

            ScheduleHeartbeat(0);
        }

        public TcpConnectionManager(string connectionName, 
                                    Guid connectionId,
                                    ITcpDispatcher dispatcher,
                                    IPublisher publisher,
                                    IPEndPoint remoteEndPoint, 
                                    TcpClientConnector connector,
                                    bool useSsl,
                                    string sslTargetHost,
                                    bool sslValidateServer,
                                    IPublisher networkSendQueue,
                                    InternalAuthenticationProvider authProvider,
                                    TimeSpan heartbeatInterval,
                                    TimeSpan heartbeatTimeout,
                                    Action<TcpConnectionManager> onConnectionEstablished,
                                    Action<TcpConnectionManager, SocketError> onConnectionClosed)
        {
            Ensure.NotEmptyGuid(connectionId, "connectionId");
            Ensure.NotNull(dispatcher, "dispatcher");
            Ensure.NotNull(publisher, "publisher");
            Ensure.NotNull(authProvider, "authProvider");
            Ensure.NotNull(remoteEndPoint, "remoteEndPoint");
            Ensure.NotNull(connector, "connector");
            if (useSsl) Ensure.NotNull(sslTargetHost, "sslTargetHost");

            ConnectionId = connectionId;
            ConnectionName = connectionName;

            _tcpEnvelope = new SendOverTcpEnvelope(this, networkSendQueue);
            _publisher = publisher;
            _dispatcher = dispatcher;
            _authProvider = authProvider;

            _framer = new LengthPrefixMessageFramer();
            _framer.RegisterMessageArrivedCallback(OnMessageArrived);

            _heartbeatInterval = heartbeatInterval;
            _heartbeatTimeout = heartbeatTimeout;

            _connectionEstablished = onConnectionEstablished;
            _connectionClosed = onConnectionClosed;

            _connection = useSsl 
                ? connector.ConnectSslTo(ConnectionId, remoteEndPoint, sslTargetHost, sslValidateServer, OnConnectionEstablished, OnConnectionFailed)
                : connector.ConnectTo(ConnectionId, remoteEndPoint, OnConnectionEstablished, OnConnectionFailed);
            _connection.ConnectionClosed += OnConnectionClosed;
            if (_connection.IsClosed)
                OnConnectionClosed(_connection, SocketError.Success);
        }

        private void OnConnectionEstablished(ITcpConnection connection)
        {
            Log.Info("Connection '{0}' ({1:B}) to [{2}] established.", ConnectionName, ConnectionId, connection.RemoteEndPoint);

            ScheduleHeartbeat(0);

            var handler = _connectionEstablished;
            if (handler != null)
                handler(this);
        }

        private void OnConnectionFailed(ITcpConnection connection, SocketError socketError)
        {
            if (Interlocked.CompareExchange(ref _isClosed, 1, 0) == 0)
            {
                Log.Info("Connection '{0}' ({1:B}) to [{2}] failed: {3}.", ConnectionName, ConnectionId, connection.RemoteEndPoint, socketError);
                if (_connectionClosed != null)
                    _connectionClosed(this, socketError);
            }
        }

        private void OnConnectionClosed(ITcpConnection connection, SocketError socketError)
        {
            if (Interlocked.CompareExchange(ref _isClosed, 1, 0) == 0)
            {
                Log.Info("Connection '{0}' [{1}, {2:B}] closed: {3}.", ConnectionName, connection.RemoteEndPoint, ConnectionId, socketError);
                if (_connectionClosed != null)
                    _connectionClosed(this, socketError);
            }
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
                ProcessPackage(package);
            }
            catch (Exception e)
            {
                SendBadRequestAndClose(package.CorrelationId, string.Format("Error during processing package. Error: {0}", e));
            }
        }

        private void ProcessPackage(TcpPackage package)
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
                    Helper.EatException(() => reason = Helper.UTF8NoBom.GetString(package.Data.Array, package.Data.Offset, package.Data.Count));
                    var exitMessage = 
                        string.Format("Bad request received from '{0}' [{1}, L{2}, {3:B}], will stop server. CorrelationId: {4:B}, Error: {5}.",
                                      ConnectionName, RemoteEndPoint, LocalEndPoint, ConnectionId, package.CorrelationId,
                                      reason.IsEmptyString() ? "<reason missing>" : reason);
                    Log.Error(exitMessage);
                    Application.Exit(ExitCode.Error, exitMessage);
                    break;
                }
                case TcpCommand.Authenticate:
                {
                    if ((package.Flags & TcpFlags.Authenticated) == 0)
                        ReplyNotAuthenticated(package.CorrelationId, "No user credentials provided.");
                    else
                    {
                        var defaultUser = new UserCredentials(package.Login, package.Password, null);
                        Interlocked.Exchange(ref _defaultUser, defaultUser);
                        _authProvider.Authenticate(new TcpDefaultAuthRequest(this, package.CorrelationId, defaultUser));
                    }
                    break;
                }
                default:
                {
                    var defaultUser = _defaultUser;
                    if ((package.Flags & TcpFlags.Authenticated) != 0)
                    {
                        _authProvider.Authenticate(new TcpAuthRequest(this, package, package.Login, package.Password));
                    }
                    else if (defaultUser != null)
                    {
                        if (defaultUser.User != null)
                            UnwrapAndPublishPackage(package, defaultUser.User, defaultUser.Login, defaultUser.Password);
                        else
                            _authProvider.Authenticate(new TcpAuthRequest(this, package, defaultUser.Login, defaultUser.Password));
                    }
                    else
                    {
                        UnwrapAndPublishPackage(package, null, null, null);
                    }
                    break;
                }
            }
        }

        private void UnwrapAndPublishPackage(TcpPackage package, IPrincipal user, string login, string password)
        {
            var message = _dispatcher.UnwrapPackage(package, _tcpEnvelope, user, login, password, this);
            if (message != null)
                _publisher.Publish(message);
            else
                SendBadRequestAndClose(package.CorrelationId,
                                       string.Format("Couldn't unwrap network package for command {0}.", package.Command));
        }

        private void ReplyNotAuthenticated(Guid correlationId, string description)
        {
            _tcpEnvelope.ReplyWith(new TcpMessage.NotAuthenticated(correlationId, description));
        }

        private void ReplyAuthenticated(Guid correlationId, UserCredentials userCredentials, IPrincipal user)
        {
            var authCredentials = new UserCredentials(userCredentials.Login, userCredentials.Password, user);
            Interlocked.CompareExchange(ref _defaultUser, authCredentials, userCredentials);
            _tcpEnvelope.ReplyWith(new TcpMessage.Authenticated(correlationId));
        }

        public void SendBadRequestAndClose(Guid correlationId, string message)
        {
            Ensure.NotNull(message, "message");

            SendPackage(new TcpPackage(TcpCommand.BadRequest, correlationId, Helper.UTF8NoBom.GetBytes(message)), checkQueueSize: false);
            Log.Error("Closing connection '{0}' [{1}, L{2}, {3:B}] due to error. Reason: {4}",
                      ConnectionName, RemoteEndPoint, LocalEndPoint, ConnectionId, message);
            _connection.Close(message);
        }

        public void Stop(string reason = null)
        {
            Log.Trace("Closing connection '{0}' [{1}, L{2}, {3:B}] cleanly.{4}",
                      ConnectionName, RemoteEndPoint, LocalEndPoint, ConnectionId,
                      reason.IsEmpty() ? string.Empty : " Reason: " + reason);
            _connection.Close(reason);
        }

        public void SendMessage(Message message)
        {
            var package = _dispatcher.WrapMessage(message);
            if (package != null)
                SendPackage(package.Value);
        }

        private void SendPackage(TcpPackage package, bool checkQueueSize = true)
        {
            var data = package.AsArraySegment();
            var framed = _framer.FrameData(data);
            _connection.EnqueueSend(framed);

            int queueSize;
            if (checkQueueSize && (queueSize = _connection.SendQueueSize) > ConnectionQueueSizeThreshold)
                SendBadRequestAndClose(Guid.Empty, string.Format("Connection queue size is too large: {0}.", queueSize));
        }

        public void Handle(TcpMessage.Heartbeat message)
        {
            if (IsClosed) return;

            var msgNum = _messageNumber;
            if (message.MessageNumber != msgNum)
                ScheduleHeartbeat(msgNum);
            else
            {
                SendPackage(new TcpPackage(TcpCommand.HeartbeatRequestCommand, Guid.NewGuid(), null));

                _publisher.Publish(TimerMessage.Schedule.Create(
                    _heartbeatTimeout,
                    new SendToWeakThisEnvelope(this), 
                    new TcpMessage.HeartbeatTimeout(RemoteEndPoint, msgNum)));
            }
        }

        public void Handle(TcpMessage.HeartbeatTimeout message)
        {
            if (IsClosed) return;

            var msgNum = _messageNumber;
            if (message.MessageNumber != msgNum)
                ScheduleHeartbeat(msgNum);
            else
                Stop(string.Format("HEARTBEAT TIMEOUT at msgNum {0}", msgNum));
        }

        private void ScheduleHeartbeat(int msgNum)
        {
            _publisher.Publish(TimerMessage.Schedule.Create(_heartbeatInterval,
                                                            new SendToWeakThisEnvelope(this),
                                                            new TcpMessage.Heartbeat(RemoteEndPoint, msgNum)));
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

        private class TcpAuthRequest: InternalAuthenticationProvider.AuthenticationRequest
        {
            private readonly TcpConnectionManager _manager;
            private readonly TcpPackage _package;

            public TcpAuthRequest(TcpConnectionManager manager, TcpPackage package, string login, string password) 
                : base(login, password)
            {
                _manager = manager;
                _package = package;
            }

            public override void Unauthorized()
            {
                _manager.ReplyNotAuthenticated(_package.CorrelationId, "Not Authenticated");
            }

            public override void Authenticated(IPrincipal principal)
            {
                _manager.UnwrapAndPublishPackage(_package, principal, Name, SuppliedPassword);
            }

            public override void Error()
            {
                _manager.ReplyNotAuthenticated(_package.CorrelationId, "Internal Server Error");
            }
        }

        private class TcpDefaultAuthRequest : InternalAuthenticationProvider.AuthenticationRequest
        {
            private readonly TcpConnectionManager _manager;
            private readonly Guid _correlationId;
            private readonly UserCredentials _userCredentials;

            public TcpDefaultAuthRequest(TcpConnectionManager manager, Guid correlationId, UserCredentials userCredentials)
                : base(userCredentials.Login, userCredentials.Password)
            {
                _manager = manager;
                _correlationId = correlationId;
                _userCredentials = userCredentials;
            }

            public override void Unauthorized()
            {
                _manager.ReplyNotAuthenticated(_correlationId, "Unauthorized");
            }

            public override void Authenticated(IPrincipal principal)
            {
                _manager.ReplyAuthenticated(_correlationId, _userCredentials, principal);
            }

            public override void Error()
            {
                _manager.ReplyNotAuthenticated(_correlationId, "Internal Server Error");
            }
        }

        private class UserCredentials
        {
            public readonly string Login;
            public readonly string Password;
            public readonly IPrincipal User;

            public UserCredentials(string login, string password, IPrincipal user)
            {
                Login = login;
                Password = password;
                User = user;
            }
        }
    }
}