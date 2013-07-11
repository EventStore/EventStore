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

namespace EventStore.ClientAPI.Transport.Tcp
{

    //TODO GFY THIS CODE NEEDS SOME LOVE ITS KIND OF CONVOLUTED HOW ITS WORKING WITH TCP CONNECTIONS
    internal class TcpClientConnector
    {
        private readonly SocketArgsPool _connectSocketArgsPool;
        private readonly List<ConnectingSocket> _connectingSockets;
        private readonly Timer _timer;
        public TcpClientConnector()
        {
            _connectSocketArgsPool = new SocketArgsPool("TcpClientConnector._connectSocketArgsPool",
                                                        TcpConfiguration.ConnectPoolSize,
                                                        CreateConnectSocketArgs);
            _connectingSockets = new List<ConnectingSocket>();
            _timer = new Timer(TimerCallback, null, 200, 200);
        }

        private void TimerCallback(object state)
        {
            List<ConnectingSocket> tokill;
            lock(_connectingSockets)
            {
                tokill = _connectingSockets.Where(
                    x =>
                        {
                            var d = DateTime.Now;
                            return x.WhenToKill < d;
                        }).ToList();
            }
            foreach(var item in tokill)
            {
                lock (_connectingSockets)
                {
                    if (_connectingSockets.FirstOrDefault(x => x.Connection == item) == null) continue;
                }
                HandleTimeout(item.Connection);
            }
        }

        private SocketAsyncEventArgs CreateConnectSocketArgs()
        {
            var socketArgs = new SocketAsyncEventArgs();
            socketArgs.Completed += ConnectCompleted;
            socketArgs.UserToken = new CallbacksStateToken();
            return socketArgs;
        }

        public ITcpConnection ConnectTo(ILogger log, 
                                        Guid connectionId,
                                        IPEndPoint remoteEndPoint, 
                                        bool ssl,
                                        string targetHost,
                                        bool validateServer,
                                        TimeSpan timeout,
                                        Action<ITcpConnection> onConnectionEstablished = null,
                                        Action<ITcpConnection, SocketError> onConnectionFailed = null,
                                        Action<ITcpConnection, SocketError> onConnectionClosed = null)
        {
            var timeoutAt = DateTime.Now.Add(timeout);
            Ensure.NotNull(remoteEndPoint, "remoteEndPoint");
            if (ssl)
            {
                Ensure.NotNullOrEmpty(targetHost, "targetHost");
                var connecting = TcpConnectionSsl.CreateConnectingConnection(log, connectionId, remoteEndPoint,
                                                                   targetHost, validateServer, 
                                                                   this, onConnectionEstablished, onConnectionFailed, onConnectionClosed);
                AddToConnecting(connecting, timeoutAt);
                return connecting;
            }
            var conn = TcpConnection.CreateConnectingConnection(log, connectionId, remoteEndPoint,
                                                            this, onConnectionEstablished, onConnectionFailed, onConnectionClosed);
            AddToConnecting(conn, timeoutAt);
            return conn;
        }

        private void AddToConnecting(ITcpConnection conn, DateTime when)
        {
            lock (_connectingSockets)
            {
                _connectingSockets.Add(new ConnectingSocket(conn, when));
            }
        }

        private void RemoveFromConnecting(ITcpConnection conn)
        {
            lock (_connectingSockets)
            {
                _connectingSockets.RemoveAll(x => x.Connection == conn);
            }
        }

        internal void InitConnect(IPEndPoint serverEndPoint,
                                  Action<IPEndPoint, Socket> onConnectionEstablished,
                                  Action<IPEndPoint, SocketError> onConnectionFailed,
                                  ITcpConnection connection)
        {
            if (serverEndPoint == null)
                throw new ArgumentNullException("serverEndPoint");
            if (onConnectionEstablished == null)
                throw new ArgumentNullException("onConnectionEstablished");
            if (onConnectionFailed == null)
                throw new ArgumentNullException("onConnectionFailed");

            var socketArgs = _connectSocketArgsPool.Get();
            var connectingSocket = new Socket(serverEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            socketArgs.RemoteEndPoint = serverEndPoint;
            socketArgs.AcceptSocket = connectingSocket;
            var callbacks = (CallbacksStateToken)socketArgs.UserToken;
            callbacks.OnConnectionEstablished = onConnectionEstablished;
            callbacks.OnConnectionFailed = onConnectionFailed;
            callbacks.Connection = connection;

            try
            {
                var firedAsync = connectingSocket.ConnectAsync(socketArgs);
                if (!firedAsync)
                    ProcessConnect(socketArgs);
            }
            catch (ObjectDisposedException)
            {
                HandleBadConnect(socketArgs);
            }
        }

        private void ConnectCompleted(object sender, SocketAsyncEventArgs e)
        {
            ProcessConnect(e);
        }

        private void ProcessConnect(SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
                HandleBadConnect(e);
            else
                OnSocketConnected(e);
        }

        private void HandleBadConnect(SocketAsyncEventArgs socketArgs)
        {
            var serverEndPoint = socketArgs.RemoteEndPoint;
            var socketError = socketArgs.SocketError;
            var callbacks = (CallbacksStateToken)socketArgs.UserToken;
            var onConnectionFailed = callbacks.OnConnectionFailed;

            Helper.EatException(() => socketArgs.AcceptSocket.Close(TcpConfiguration.SocketCloseTimeoutMs));
            RemoveFromConnecting(callbacks.Connection);
            socketArgs.AcceptSocket = null;
            callbacks.Reset();

            _connectSocketArgsPool.Return(socketArgs);

            onConnectionFailed((IPEndPoint)serverEndPoint, socketError);
        }

        private void HandleTimeout(ITcpConnection connection)
        {
            RemoveFromConnecting(connection);
            Helper.EatException(() => connection.Close("Timeout on occured connect."));
        }

        private void OnSocketConnected(SocketAsyncEventArgs socketArgs)
        {
            var remoteEndPoint = (IPEndPoint) socketArgs.RemoteEndPoint;
            var socket = socketArgs.AcceptSocket;
            var callbacks = (CallbacksStateToken)socketArgs.UserToken;
            var onConnectionEstablished = callbacks.OnConnectionEstablished;
            socketArgs.AcceptSocket = null;
            RemoveFromConnecting(callbacks.Connection);
            callbacks.Reset();
            _connectSocketArgsPool.Return(socketArgs);
            
            onConnectionEstablished(remoteEndPoint, socket);
        }

        private class CallbacksStateToken
        {
            public Action<IPEndPoint, Socket> OnConnectionEstablished;
            public Action<IPEndPoint, SocketError> OnConnectionFailed;
            public ITcpConnection Connection;

            public void Reset()
            {
                OnConnectionEstablished = null;
                OnConnectionFailed = null;
                Connection = null;
            }
        }
    }

    class ConnectingSocket
    {
        public readonly ITcpConnection Connection;
        public readonly DateTime WhenToKill;
        public ConnectingSocket(ITcpConnection connection, DateTime whenToKill)
        {
            Connection = connection;
            WhenToKill = whenToKill;
        }
    }
}