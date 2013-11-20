﻿// Copyright (c) 2012, Event Store LLP
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
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using EventStore.ClientAPI.Common;
using EventStore.ClientAPI.Common.Utils;

namespace EventStore.ClientAPI.Transport.Tcp
{
    internal class TcpConnection : TcpConnectionBase, ITcpConnection
    {
        internal const int MaxSendPacketSize = 64 * 1024;

        internal static readonly SocketArgsPool SocketArgsPool = new SocketArgsPool("TcpConnection.SocketArgsPool", 
                                                                                    TcpConfiguration.SendReceivePoolSize, 
                                                                                    () => new SocketAsyncEventArgs());

        internal static ITcpConnection CreateConnectingConnection(ILogger log,
                                                                  Guid connectionId, 
                                                                  IPEndPoint remoteEndPoint,
                                                                  TcpClientConnector connector,
                                                                  TimeSpan connectionTimeout,
                                                                  Action<ITcpConnection> onConnectionEstablished,
                                                                  Action<ITcpConnection, SocketError> onConnectionFailed,
                                                                  Action<ITcpConnection, SocketError> onConnectionClosed)
        {
            var connection = new TcpConnection(log, connectionId, remoteEndPoint, onConnectionClosed);
// ReSharper disable ImplicitlyCapturedClosure
            connector.InitConnect(remoteEndPoint,
                                  (_, socket) =>
                                  {
                                      connection.InitSocket(socket);
                                      if (onConnectionEstablished != null)
                                          onConnectionEstablished(connection);
                                  },
                                  (_, socketError) =>
                                  {
                                      if (onConnectionFailed != null)
                                          onConnectionFailed(connection, socketError);
                                  }, connection, connectionTimeout);
// ReSharper restore ImplicitlyCapturedClosure
            return connection;
        }

        public Guid ConnectionId { get { return _connectionId; } }
        public int SendQueueSize { get { return _sendQueue.Count; } }

        private readonly Guid _connectionId;
        private readonly ILogger _log;

        private Socket _socket;
        private SocketAsyncEventArgs _receiveSocketArgs;
        private SocketAsyncEventArgs _sendSocketArgs;

        private readonly Common.Concurrent.ConcurrentQueue<ArraySegment<byte>> _sendQueue = new Common.Concurrent.ConcurrentQueue<ArraySegment<byte>>();
        private readonly Queue<ArraySegment<byte>> _receiveQueue = new Queue<ArraySegment<byte>>();
        private readonly MemoryStream _memoryStream = new MemoryStream();

        private readonly object _receivingLock = new object();
        private readonly SpinLock2 _sendingLock = new SpinLock2();
        private bool _isSending;
        private volatile int _closed;

        private Action<ITcpConnection, IEnumerable<ArraySegment<byte>>> _receiveCallback;
        private readonly Action<ITcpConnection, SocketError> _onConnectionClosed;

        private TcpConnection(ILogger log, Guid connectionId, IPEndPoint remoteEndPoint, Action<ITcpConnection, SocketError> onConnectionClosed)
            : base(remoteEndPoint)
        {
            Ensure.NotNull(log, "log");
            Ensure.NotEmptyGuid(connectionId, "connectionId");

            _connectionId = connectionId;
            _log = log;
            _onConnectionClosed = onConnectionClosed;
        }

        private void InitSocket(Socket socket)
        {
            InitConnectionBase(socket);
            //_log.Info("TcpConnection::InitSocket[{0}, L{1}]", RemoteEndPoint, LocalEndPoint);
            using (_sendingLock.Acquire())
            {
                _socket = socket;
                try
                {
                    socket.NoDelay = true;
                }
                catch (ObjectDisposedException)
                {
                    CloseInternal(SocketError.Shutdown, "Socket disposed.");
                    _socket = null;
                    return;
                }

                var receiveSocketArgs = SocketArgsPool.Get();
                _receiveSocketArgs = receiveSocketArgs;
                _receiveSocketArgs.AcceptSocket = socket;
                _receiveSocketArgs.Completed += OnReceiveAsyncCompleted;

                var sendSocketArgs = SocketArgsPool.Get();
                _sendSocketArgs = sendSocketArgs;
                _sendSocketArgs.AcceptSocket = socket;
                _sendSocketArgs.Completed += OnSendAsyncCompleted;
            }
            StartReceive();
            TrySend();
        }

        public void EnqueueSend(IEnumerable<ArraySegment<byte>> data)
        {
            using (_sendingLock.Acquire())
            {
                int bytes = 0;
                foreach (var segment in data)
                {
                    _sendQueue.Enqueue(segment);
                    bytes += segment.Count;
                }
                NotifySendScheduled(bytes);
            }
            TrySend();
        }

        private void TrySend()
        {
            using (_sendingLock.Acquire())
            {
                if (_isSending || _sendQueue.Count == 0 || _socket == null) return;
                if (TcpConnectionMonitor.Default.IsSendBlocked()) return;
                _isSending = true;
            }

            _memoryStream.SetLength(0);

            ArraySegment<byte> sendPiece;
            while (_sendQueue.TryDequeue(out sendPiece))
            {
                _memoryStream.Write(sendPiece.Array, sendPiece.Offset, sendPiece.Count);
                if (_memoryStream.Length >= MaxSendPacketSize)
                    break;
            }

            _sendSocketArgs.SetBuffer(_memoryStream.GetBuffer(), 0, (int) _memoryStream.Length);

            try
            {
                NotifySendStarting(_sendSocketArgs.Count);
                var firedAsync = _sendSocketArgs.AcceptSocket.SendAsync(_sendSocketArgs);
                if (!firedAsync)
                    ProcessSend(_sendSocketArgs);
            }
            catch (ObjectDisposedException)
            {
                ReturnSendingSocketArgs();
            }
        }

        private void OnSendAsyncCompleted(object sender, SocketAsyncEventArgs e)
        {
            // No other code should go here. All handling is the same for sync/async completion.
            ProcessSend(e);
        }

        private void ProcessSend(SocketAsyncEventArgs socketArgs)
        {
            if (socketArgs.SocketError != SocketError.Success)
            {
                NotifySendCompleted(0);
                ReturnSendingSocketArgs();
                CloseInternal(socketArgs.SocketError, "Socket send error.");
            }
            else
            {
                NotifySendCompleted(socketArgs.Count);
                if (_closed != 0)
                    ReturnSendingSocketArgs();
                else
                {
                    using (_sendingLock.Acquire())
                    {
                        _isSending = false;
                    }
                    TrySend();
                }
            }
        }

        public void ReceiveAsync(Action<ITcpConnection, IEnumerable<ArraySegment<byte>>> callback)
        {
            if (callback == null)
                throw new ArgumentNullException("callback");

            lock (_receivingLock)
            {
                if (_receiveCallback != null)
                    throw new InvalidOperationException("ReceiveAsync called again while previous call wasn't fulfilled");
                _receiveCallback = callback;
            }

            TryDequeueReceivedData();
        }

        private void StartReceive()
        {
            var buffer = new ArraySegment<byte>(new byte[TcpConfiguration.SocketBufferSize]);
            _receiveSocketArgs.SetBuffer(buffer.Array, buffer.Offset, buffer.Count);
            try
            {
                NotifyReceiveStarting();
                bool firedAsync = _receiveSocketArgs.AcceptSocket.ReceiveAsync(_receiveSocketArgs);
                if (!firedAsync)
                    ProcessReceive(_receiveSocketArgs);
            }
            catch (ObjectDisposedException)
            {
                ReturnReceivingSocketArgs();
            }
        }

        private void OnReceiveAsyncCompleted(object sender, SocketAsyncEventArgs e)
        {
            // No other code should go here.  All handling is the same on async and sync completion.
            ProcessReceive(e);
        }

        private void ProcessReceive(SocketAsyncEventArgs socketArgs)
        {
            // socket closed normally or some error occurred
            if (socketArgs.BytesTransferred == 0 || socketArgs.SocketError != SocketError.Success)
            {
                NotifyReceiveCompleted(0);
                ReturnReceivingSocketArgs();
                CloseInternal(socketArgs.SocketError, socketArgs.SocketError != SocketError.Success ? "Socket receive error" : "Socket closed");
                return;
            }

            NotifyReceiveCompleted(socketArgs.BytesTransferred);

            var receiveBuffer = new ArraySegment<byte>(socketArgs.Buffer, socketArgs.Offset, socketArgs.BytesTransferred);
            lock (_receivingLock)
            {
                _receiveQueue.Enqueue(receiveBuffer);
            }

            lock (_receiveSocketArgs)
            {
                if (socketArgs.Buffer == null)
                    throw new Exception("Cleaning already null buffer");
                socketArgs.SetBuffer(null, 0, 0);
            }

            StartReceive();
            TryDequeueReceivedData();
        }

        private void TryDequeueReceivedData()
        {
            Action<ITcpConnection, IEnumerable<ArraySegment<byte>>> callback;
            List<ArraySegment<byte>> res;
            lock (_receivingLock)
            {
                // no awaiting callback or no data to dequeue
                if (_receiveCallback == null || _receiveQueue.Count == 0)
                    return;

                res = new List<ArraySegment<byte>>(_receiveQueue.Count);
                while (_receiveQueue.Count > 0)
                {
                    res.Add(_receiveQueue.Dequeue());
                }

                callback = _receiveCallback;
                _receiveCallback = null;
            }

            callback(this, res);

            int bytes = 0;
            for (int i = 0, n = res.Count; i < n; ++i)
            {
                bytes += res[i].Count;
            }
            NotifyReceiveDispatched(bytes);
        }

        public void Close(string reason)
        {
            CloseInternal(SocketError.Success, reason ?? "Normal socket close."); // normal socket closing
        }

        private void CloseInternal(SocketError socketError, string reason)
        {
#pragma warning disable 420
            if (Interlocked.CompareExchange(ref _closed, 1, 0) != 0)
                return;
#pragma warning restore 420

            NotifyClosed();

            _log.Info("ClientAPI {12} closed [{0:HH:mm:ss.fff}: N{1}, L{2}, {3:B}]:\nReceived bytes: {4}, Sent bytes: {5}\n"
                      + "Send calls: {6}, callbacks: {7}\nReceive calls: {8}, callbacks: {9}\nClose reason: [{10}] {11}\n",
                      DateTime.UtcNow, RemoteEndPoint, LocalEndPoint, _connectionId,
                      TotalBytesReceived, TotalBytesSent,
                      SendCalls, SendCallbacks,
                      ReceiveCalls, ReceiveCallbacks,
                      socketError, reason, GetType().Name);

            if (_socket != null)
            {
                Helper.EatException(() => _socket.Shutdown(SocketShutdown.Both));
                Helper.EatException(() => _socket.Close(TcpConfiguration.SocketCloseTimeoutMs));
                _socket = null;
            }

            using (_sendingLock.Acquire())
            {
                if (!_isSending)
                    ReturnSendingSocketArgs();
            }

            if (_onConnectionClosed != null)
                _onConnectionClosed(this, socketError);
        }

        private void ReturnSendingSocketArgs()
        {
            var socketArgs = Interlocked.Exchange(ref _sendSocketArgs, null);
            if (socketArgs != null)
            {
                socketArgs.Completed -= OnSendAsyncCompleted;
                socketArgs.AcceptSocket = null;
                if (socketArgs.Buffer != null)
                    socketArgs.SetBuffer(null, 0, 0);
                SocketArgsPool.Return(socketArgs);
            }
        }

        private void ReturnReceivingSocketArgs()
        {
            var socketArgs = Interlocked.Exchange(ref _receiveSocketArgs, null);
            if (socketArgs != null)
            {
                socketArgs.Completed -= OnReceiveAsyncCompleted;
                socketArgs.AcceptSocket = null;
                if (socketArgs.Buffer != null)
                    socketArgs.SetBuffer(null, 0, 0);
                SocketArgsPool.Return(socketArgs);
            }
        }

        public override string ToString()
        {
            return RemoteEndPoint.ToString();
        }
    }
}