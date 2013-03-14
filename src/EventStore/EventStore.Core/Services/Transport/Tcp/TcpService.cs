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
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Transport.Tcp;

namespace EventStore.Core.Services.Transport.Tcp
{
    public enum TcpServiceType
    {
        Internal, 
        External
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
        private readonly Func<Guid, IPEndPoint, ITcpDispatcher> _dispatcherFactory;

        public TcpService(IPublisher publisher,
                          IPEndPoint serverEndPoint,
                          IPublisher networkSendQueue,
                          TcpServiceType serviceType,
                          ITcpDispatcher dispatcher)
            : this(publisher, serverEndPoint, networkSendQueue, serviceType, (_, __) => dispatcher)
        {
        }

        public TcpService(IPublisher publisher, 
                          IPEndPoint serverEndPoint, 
                          IPublisher networkSendQueue, 
                          TcpServiceType serviceType,
                          Func<Guid, IPEndPoint, ITcpDispatcher> dispatcherFactory)
        {
            Ensure.NotNull(publisher, "publisher");
            Ensure.NotNull(serverEndPoint, "serverEndPoint");
            Ensure.NotNull(networkSendQueue, "networkSendQueue");
            Ensure.NotNull(dispatcherFactory, "dispatcherFactory");

            _publisher = publisher;
            _serverEndPoint = serverEndPoint;
            _serverListener = new TcpServerListener(_serverEndPoint);
            _networkSendQueue = networkSendQueue;
            _serviceType = serviceType;
            _dispatcherFactory = dispatcherFactory;
        }

        public void Handle(SystemMessage.SystemInit message)
        {
            try
            {
                _serverListener.StartListening(OnConnectionAccepted);
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

        private void OnConnectionAccepted(TcpConnection connection)
        {
            Log.Info("{0} TCP connection accepted: [{1}], connection ID: {2}.", _serviceType, connection.EffectiveEndPoint, connection.ConnectionId);
            
            var dispatcher = _dispatcherFactory(connection.ConnectionId, _serverEndPoint);
            var manager = new TcpConnectionManager(_serviceType.ToString().ToLower(), dispatcher, _publisher, connection, _networkSendQueue);
            manager.ConnectionClosed += OnConnectionClosed;

            _publisher.Publish(new TcpMessage.ConnectionEstablished(manager));
            manager.StartReceiving();
        }

        private void OnConnectionClosed(TcpConnectionManager manager, SocketError socketError)
        {
            manager.ConnectionClosed -= OnConnectionClosed;
            _publisher.Publish(new TcpMessage.ConnectionClosed(manager, socketError));
        }
    }
}
