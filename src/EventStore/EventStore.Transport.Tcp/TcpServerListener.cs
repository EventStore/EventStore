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

namespace EventStore.Transport.Tcp
{
    public class TcpServerListener
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<TcpServerListener>();

        private readonly IPEndPoint _serverEndPoint;
        private readonly Socket _listeningSocket;
        private readonly SocketArgsPool _acceptSocketArgsPool;
        private Action<TcpConnection> _onConnectionAccepted;

        public TcpServerListener(IPEndPoint serverEndPoint)
        {
            Ensure.NotNull(serverEndPoint, "serverEndPoint");

            _serverEndPoint = serverEndPoint;

            _listeningSocket = new Socket(serverEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            _acceptSocketArgsPool = new SocketArgsPool("TcpServerListener.AcceptSocketArgsPool",
                                                       TcpConfiguration.ConcurrentAccepts*2,
                                                       CreateAcceptSocketArgs);
        }

        private SocketAsyncEventArgs CreateAcceptSocketArgs()
        {
            var socketArgs = new SocketAsyncEventArgs();
            socketArgs.Completed += AcceptCompleted;
            return socketArgs;
        }

        public void StartListening(Action<TcpConnection> callback)
        {
            Ensure.NotNull(callback, "callback");

            _onConnectionAccepted = callback;

            Log.Info("Starting TCP listening on TCP endpoint: {0}.", _serverEndPoint);
            try
            {
                _listeningSocket.Bind(_serverEndPoint);
                _listeningSocket.Listen(TcpConfiguration.AcceptBacklogCount);
            }
            catch (Exception)
            {
                Log.Info("Failed to listen on TCP endpoint: {0}.", _serverEndPoint);
                Helper.EatException(() => _listeningSocket.Close(TcpConfiguration.SocketCloseTimeoutMs));
                throw;
            }

            for (int i = 0; i < TcpConfiguration.ConcurrentAccepts; ++i)
            {
                StartAccepting();
            }
        }

        private void StartAccepting()
        {
            var socketArgs = _acceptSocketArgsPool.Get();

            try
            {
                var firedAsync = _listeningSocket.AcceptAsync(socketArgs);
                if (!firedAsync)
                    ProcessAccept(socketArgs);
            }
            catch (ObjectDisposedException)
            {
                HandleBadAccept(socketArgs);
            }
        }

        private void AcceptCompleted(object sender, SocketAsyncEventArgs e)
        {
            ProcessAccept(e);
        }

        private void ProcessAccept(SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                HandleBadAccept(e);
            }
            else
            {
                var acceptSocket = e.AcceptSocket;
                e.AcceptSocket = null;
                _acceptSocketArgsPool.Return(e);

                OnSocketAccepted(acceptSocket);
            }

            StartAccepting();
        }

        private void HandleBadAccept(SocketAsyncEventArgs socketArgs)
        {
            Helper.EatException(
                () =>
                    {
                        if (socketArgs.AcceptSocket != null) // avoid annoying exceptions
                            socketArgs.AcceptSocket.Close(TcpConfiguration.SocketCloseTimeoutMs);
                    });
            socketArgs.AcceptSocket = null;
            _acceptSocketArgsPool.Return(socketArgs);
        }

        private void OnSocketAccepted(Socket socket)
        {
            IPEndPoint socketEndPoint;
            try
            {
                socketEndPoint = (IPEndPoint)socket.RemoteEndPoint;
            }
            catch (Exception)
            {
                return;
            }

            var tcpConnection = TcpConnection.CreateAcceptedTcpConnection(Guid.NewGuid(), socketEndPoint, socket, verbose: true);
            _onConnectionAccepted(tcpConnection);
        }

        public void Stop()
        {
            Helper.EatException(() => _listeningSocket.Close(TcpConfiguration.SocketCloseTimeoutMs));
        }
    }
}