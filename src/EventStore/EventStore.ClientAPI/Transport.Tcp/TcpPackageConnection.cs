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
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI.Transport.Tcp
{
    internal class TcpPackageConnection
    {
        private static readonly ArraySegment<byte>[] HeartbeatResponse = new LengthPrefixMessageFramer().FrameData(
            new TcpPackage(TcpCommand.HeartbeatResponseCommand, Guid.NewGuid(), null).AsArraySegment()).ToArray();
        private static readonly TcpClientConnector Connector = new TcpClientConnector();

        public int SendQueueSize { get { return _connection.SendQueueSize; } }
        public readonly IPEndPoint EffectiveEndPoint;
        public readonly Guid ConnectionId;

        private readonly ILogger _log;
        private readonly Action<TcpPackageConnection, TcpPackage> _handlePackage;
        private readonly Action<TcpPackageConnection, Exception> _onError;
        private readonly Action<TcpPackageConnection, IPEndPoint, SocketError> _connectionClosed;

        private readonly LengthPrefixMessageFramer _framer;
        private readonly TcpConnection _connection;

        public TcpPackageConnection(ILogger log,
                                    IPEndPoint tcpEndPoint,
                                    Guid connectionId,
                                    Action<TcpPackageConnection, TcpPackage> handlePackage,
                                    Action<TcpPackageConnection, Exception> onError,
                                    Action<TcpPackageConnection> connectionEstablished,
                                    Action<TcpPackageConnection, IPEndPoint, SocketError> connectionClosed)
        {
            Ensure.NotNull(log, "log");
            Ensure.NotNull(tcpEndPoint, "tcpEndPoint");
            Ensure.NotEmptyGuid(connectionId, "connectionId");
            Ensure.NotNull(handlePackage, "handlePackage");

            EffectiveEndPoint = tcpEndPoint;
            ConnectionId = connectionId;
            _log = log;
            _handlePackage = handlePackage;
            _onError = onError;
            _connectionClosed = connectionClosed;

            //Setup callback for incoming messages
            _framer = new LengthPrefixMessageFramer();
            _framer.RegisterMessageArrivedCallback(IncomingMessageArrived);

            var connectionCreated = new ManualResetEventSlim();
            _connection = Connector.ConnectTo(
                tcpEndPoint,
                tcpConnection =>
                {
                    connectionCreated.Wait();
                    log.Debug("Connected to [{0}, {1:B}].", tcpConnection.EffectiveEndPoint, connectionId);
                    if (connectionEstablished != null)
                        connectionEstablished(this);
                },
                (conn, error) =>
                {
                    connectionCreated.Wait();
                    log.Debug("Connection to [{0}, {1:B}] failed. Error: {2}.", conn.EffectiveEndPoint, connectionId, error);
                    if (connectionClosed != null)
                        connectionClosed(this, conn.EffectiveEndPoint, error);
                },
                (conn, error) =>
                {
                    connectionCreated.Wait();
                    _log.Debug("Connection [{0}, {1:B}] was closed {2}",
                               conn.EffectiveEndPoint, ConnectionId, error == SocketError.Success ? "cleanly." : "with error: " + error + ".");

                    if (_connectionClosed != null)
                        _connectionClosed(this, EffectiveEndPoint, error);
                });

            connectionCreated.Set();
        }

        private void OnRawDataReceived(ITcpConnection connection, IEnumerable<ArraySegment<byte>> data)
        {
            try
            {
                _framer.UnFrameData(data);
            }
            catch (PackageFramingException exc)
            {
                _log.Error(exc, "Invalid TCP frame received.");
                Close();
                return;
            }

            connection.ReceiveAsync(OnRawDataReceived);
        }

        private void IncomingMessageArrived(ArraySegment<byte> data)
        {
            var package = new TcpPackage();
            var valid = false;
            try
            {
                package = TcpPackage.FromArraySegment(data);
                valid = true;

                if (package.Command == TcpCommand.HeartbeatRequestCommand)
                {
                    _connection.EnqueueSend(HeartbeatResponse);
                    return;
                }

                _handlePackage(this, package);
            }
            catch (Exception e)
            {
                _connection.Close();

                var message = string.Format("[{0}] ERROR for {1}. Connection will be closed.",
                                            EffectiveEndPoint, valid ? package.Command.ToString() : "<invalid package>");
                if (_onError != null)
                    _onError(this, e);
                _log.Debug(e, message);
            }
        }

        public void StartReceiving()
        {
            if (_connection == null)
                throw new InvalidOperationException("Failed connection.");
            _connection.ReceiveAsync(OnRawDataReceived);            
        }

        public void EnqueueSend(TcpPackage package)
        {
            if (_connection == null)
                throw new InvalidOperationException("Failed connection.");
            _connection.EnqueueSend(_framer.FrameData(package.AsArraySegment()));
        }

        public void Close()
        {
            if (_connection == null)
                throw new InvalidOperationException("Failed connection.");
            _connection.Close();
        }
    }
}