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
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.Projections.Core.Messages;
using EventStore.Transport.Tcp;

namespace EventStore.Projections.Core.Services
{
    public class ProjectionClientConnectionManager :
        IHandle<SystemMessage.SystemInit>,
        IHandle<SystemMessage.BecomeShuttingDown>
    {
        private readonly ILogger _logger = LogManager.GetLoggerFor<ProjectionClientConnectionManager>();

        private readonly IPublisher _bus;
        private readonly string _ip;
        private readonly int _port;
        private readonly IPublisher _networkSendQueue;
        private bool _running;

        private TcpConnectionManager _connectionManager;

        public ProjectionClientConnectionManager(IPublisher bus, string ip, int port, IPublisher networkSendQueue)
        {
            Ensure.NotNull(networkSendQueue, "networkSendQueue");

            _bus = bus;
            _ip = ip;
            _port = port;
            _networkSendQueue = networkSendQueue;
        }

        private void Connect()
        {
            // TODO: implement some kind of master discovery
            var endPoint = new IPEndPoint(IPAddress.Parse(_ip), _port);

            var clientConnector = new TcpClientConnector();
            _connectionManager = new TcpConnectionManager(
                "projection",
                Guid.NewGuid(),
                new ClientTcpDispatcher(),
                _bus,
                endPoint,
                clientConnector,
                _networkSendQueue);

            _connectionManager.ConnectionEstablished += manager =>
                {
                    Console.WriteLine("Connection established: " + manager.EndPoint);
                    _bus.Publish(new ProjectionCoreServiceMessage.Start());
                    _bus.Publish(new ProjectionCoreServiceMessage.Connected(connection: manager));
                    _connectionManager.ConnectionClosed += OnConnectionClosed;
                    _connectionManager.StartReceiving();
                };
        }

        private void OnConnectionClosed(TcpConnectionManager connectionManager, SocketError error)
        {
            if (_running)
            {
                _bus.Publish(new ProjectionCoreServiceMessage.Stop()); //TODO: duplicate stop sent here
                Thread.Sleep(1000); //TODO: use scheduler service
                Connect();
            }
        }

        public void Handle(SystemMessage.SystemInit message)
        {
            _running = true;
            Connect();
        }

        public void Handle(SystemMessage.BecomeShuttingDown message)
        {
            _bus.Publish(new ProjectionCoreServiceMessage.Stop());
            _running = false;
            _connectionManager.Stop("Node is shutting down.");
        }
    }
}