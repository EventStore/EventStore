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
using EventStore.ClientAPI.Tcp;

namespace EventStore.ClientAPI.Transport.Tcp
{
    class TcpClientConnector
    {
       private readonly SocketArgsPool _connectSocketArgsPool;

        public TcpClientConnector()
        {
            _connectSocketArgsPool = new SocketArgsPool("TcpClientConnector._connectSocketArgsPool",
                                                        TcpConfiguration.ConnectPoolSize,
                                                        CreateConnectSocketArgs);
        }

        private SocketAsyncEventArgs CreateConnectSocketArgs()
        {
            var socketArgs = new SocketAsyncEventArgs();
            socketArgs.Completed += ConnectCompleted;
            socketArgs.UserToken = new CallbacksToken();
            return socketArgs;
        }

        public TcpConnection ConnectTo(IPEndPoint remoteEndPoint, 
                                            Action<TcpConnection> onConnectionEstablished = null,
                                            Action<TcpConnection, SocketError> onConnectionFailed = null)
        {
            if (remoteEndPoint == null) 
                throw new ArgumentNullException("remoteEndPoint");
            return TcpConnection.CreateConnectingTcpConnection(remoteEndPoint, this, onConnectionEstablished, onConnectionFailed);
        }

        internal void InitConnect(IPEndPoint serverEndPoint,
                                  Action<IPEndPoint, Socket> onConnectionEstablished,
                                  Action<IPEndPoint, SocketError> onConnectionFailed)
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
            var callbacks = (CallbacksToken)socketArgs.UserToken;
            callbacks.OnConnectionEstablished = onConnectionEstablished;
            callbacks.OnConnectionFailed = onConnectionFailed;

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
            var callbacks = (CallbacksToken)socketArgs.UserToken;
            var onConnectionFailed = callbacks.OnConnectionFailed;

            try {
               socketArgs.AcceptSocket.Close(TcpConfiguration.SocketCloseTimeoutMs);
            }
            catch(Exception)
            {
            }

            socketArgs.AcceptSocket = null;
            callbacks.Reset();

            _connectSocketArgsPool.Return(socketArgs);

            onConnectionFailed((IPEndPoint)serverEndPoint, socketError);
        }

        private void OnSocketConnected(SocketAsyncEventArgs socketArgs)
        {
            var remoteEndPoint = (IPEndPoint) socketArgs.RemoteEndPoint;
            var socket = socketArgs.AcceptSocket;
            var callbacks = (CallbacksToken)socketArgs.UserToken;
            var onConnectionEstablished = callbacks.OnConnectionEstablished;
            socketArgs.AcceptSocket = null;

            callbacks.Reset();
            _connectSocketArgsPool.Return(socketArgs);

            onConnectionEstablished(remoteEndPoint, socket);
        }

        private class CallbacksToken
        {
            public Action<IPEndPoint, Socket> OnConnectionEstablished;
            public Action<IPEndPoint, SocketError> OnConnectionFailed;

            public void Reset()
            {
                OnConnectionEstablished = null;
                OnConnectionFailed = null;
            }
        }
    }
}