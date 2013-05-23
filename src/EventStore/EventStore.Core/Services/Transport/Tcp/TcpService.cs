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
using System.Security.Cryptography.X509Certificates;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Http.Authentication;
using EventStore.Transport.Tcp;

namespace EventStore.Core.Services.Transport.Tcp
{
    public enum TcpServiceType
    {
        Internal, 
        External
    }

    public enum TcpSecurityType
    {
        Normal,
        Secure
    }

    public class TcpService : IHandle<SystemMessage.SystemInit>,
                              IHandle<SystemMessage.SystemStart>,
                              IHandle<SystemMessage.BecomeShuttingDown>
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<TcpService>();

        private readonly IPublisher _publisher;
        private readonly IPEndPoint _serverEndPoint;
        private readonly TcpServerListener _serverListener;
        private readonly IPublisher _networkSendQueue;
        private readonly TcpServiceType _serviceType;
        private readonly TcpSecurityType _securityType;
        private readonly Func<Guid, IPEndPoint, ITcpDispatcher> _dispatcherFactory;
        private readonly TimeSpan _heartbeatInterval;
        private readonly TimeSpan _heartbeatTimeout;
        private readonly InternalAuthenticationProvider _authProvider;
        private readonly X509Certificate _certificate;

        public TcpService(IPublisher publisher,
                          IPEndPoint serverEndPoint,
                          IPublisher networkSendQueue,
                          TcpServiceType serviceType,
                          TcpSecurityType securityType,
                          ITcpDispatcher dispatcher,
                          TimeSpan heartbeatInterval,
                          TimeSpan heartbeatTimeout,
                          InternalAuthenticationProvider authProvider,
                          X509Certificate certificate)
            : this(publisher, serverEndPoint, networkSendQueue, serviceType, securityType, (_, __) => dispatcher, 
                   heartbeatInterval, heartbeatTimeout, authProvider, certificate)
        {
        }

        public TcpService(IPublisher publisher, 
                          IPEndPoint serverEndPoint, 
                          IPublisher networkSendQueue, 
                          TcpServiceType serviceType,
                          TcpSecurityType securityType,
                          Func<Guid, IPEndPoint, ITcpDispatcher> dispatcherFactory,
                          TimeSpan heartbeatInterval,
                          TimeSpan heartbeatTimeout,
                          InternalAuthenticationProvider authProvider,
                          X509Certificate certificate)
        {
            Ensure.NotNull(publisher, "publisher");
            Ensure.NotNull(serverEndPoint, "serverEndPoint");
            Ensure.NotNull(networkSendQueue, "networkSendQueue");
            Ensure.NotNull(dispatcherFactory, "dispatcherFactory");
            Ensure.NotNull(authProvider, "authProvider");
            if (securityType == TcpSecurityType.Secure)
                Ensure.NotNull(certificate, "certificate");

            _publisher = publisher;
            _serverEndPoint = serverEndPoint;
            _serverListener = new TcpServerListener(_serverEndPoint);
            _networkSendQueue = networkSendQueue;
            _serviceType = serviceType;
            _securityType = securityType;
            _dispatcherFactory = dispatcherFactory;
            _heartbeatInterval = heartbeatInterval;
            _heartbeatTimeout = heartbeatTimeout;
            _authProvider = authProvider;
            _certificate = certificate;
        }

        public void Handle(SystemMessage.SystemInit message)
        {
            try
            {
                _serverListener.StartListening(OnConnectionAccepted, _securityType.ToString());
            }
            catch (Exception e)
            {
                Application.Exit(ExitCode.Error, e.Message);
            }
        }

        public void Handle(SystemMessage.SystemStart message)
        {
        }

        public void Handle(SystemMessage.BecomeShuttingDown message)
        {
            _serverListener.Stop();
        }

        private void OnConnectionAccepted(IPEndPoint endPoint, Socket socket)
        {
            var conn = _securityType == TcpSecurityType.Secure 
                ? TcpConnectionSsl.CreateServerFromSocket(Guid.NewGuid(), endPoint, socket, _certificate, verbose: true)
                : TcpConnection.CreateAcceptedTcpConnection(Guid.NewGuid(), endPoint, socket, verbose: true);
            Log.Info("{0} TCP connection accepted: [{1}, {2}], connection ID: {3}.", 
                     _serviceType, _securityType, conn.EffectiveEndPoint, conn.ConnectionId);

            var dispatcher = _dispatcherFactory(conn.ConnectionId, _serverEndPoint);
            var manager = new TcpConnectionManager(
                    string.Format("{0}-{1}", _serviceType.ToString().ToLower(), _securityType.ToString().ToLower()),
                    dispatcher,
                    _publisher,
                    conn,
                    _networkSendQueue,
                    _authProvider,
                    _heartbeatInterval,
                    _heartbeatTimeout,
                    onConnectionClosed: (m, e) => _publisher.Publish(new TcpMessage.ConnectionClosed(m, e))); // TODO AN: race condition
            _publisher.Publish(new TcpMessage.ConnectionEstablished(manager));
            manager.StartReceiving();
        }
    }
}
