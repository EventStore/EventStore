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
using EventStore.ClientAPI.Common;
using EventStore.ClientAPI.Common.Log;
using EventStore.ClientAPI.Defines;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.Tcp;
using EventStore.ClientAPI.Transport.Tcp;
using Connection = EventStore.ClientAPI.Transport.Tcp.TcpTypedConnection;

namespace EventStore.ClientAPI
{
     class TcpConnector
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<TcpConnector>();

        private readonly TcpClientConnector _connector = new TcpClientConnector();
        private readonly IPEndPoint _tcpEndpoint;

        public TcpConnector(IPEndPoint tcpEndpoint)
        {
            _tcpEndpoint = tcpEndpoint;
        }

        public Connection CreateTcpConnection(Action<Connection, TcpPackage> handlePackage,
                                              Action<Connection> connectionEstablished,
                                              Action<Connection, IPEndPoint, SocketError> connectionClosed)
        {
            var connectionCreatedEvent = new AutoResetEvent(false);
            Connection typedConnection = null;

            var connection = _connector.ConnectTo(
                _tcpEndpoint,
                tcpConnection =>
                {
                    Log.Info("Connected to [{0}].", tcpConnection.EffectiveEndPoint);
                    connectionCreatedEvent.WaitOne(500);
                    connectionEstablished(typedConnection);
                },
                (conn, error) =>
                {
                    var message = string.Format("Connection to [{0}] failed. Error: {1}.", conn.EffectiveEndPoint, error);
                    Log.Error(message);

                    connectionClosed(null, conn.EffectiveEndPoint, error);
                });

            typedConnection = new Connection(connection);
            typedConnection.ConnectionClosed +=
                (conn, error) =>
                {
                    Log.Info("Connection [{0}] was closed {1}",
                             conn.EffectiveEndPoint,
                             error == SocketError.Success ? "cleanly." : "with error: " + error + ".");

                    connectionClosed(conn, conn.EffectiveEndPoint, error);
                };

            connectionCreatedEvent.Set();

            typedConnection.ReceiveAsync((conn, pkg) =>
            {
                var package = new TcpPackage();
                var valid = false;
                try
                {
                    package = TcpPackage.FromArraySegment(new ArraySegment<byte>(pkg));
                    valid = true;

                    if (package.Command == TcpCommand.HeartbeatRequestCommand)
                    {
                        var response = new TcpPackage(TcpCommand.HeartbeatResponseCommand, Guid.NewGuid(), null);
                        conn.EnqueueSend(response.AsByteArray());
                        return;
                    }

                    handlePackage(conn, package);
                }
                catch (Exception e)
                {
                    var effectiveEndPoint = conn.EffectiveEndPoint;
                    var message = string.Format("[{0}] ERROR for {1}. Connection will be closed.",
                                      effectiveEndPoint,
                                      valid ? package.Command as object : "<invalid package>");

                    Log.Info(e, message);
                    conn.Close();
                }
            });

            return typedConnection;
        }
    }
}