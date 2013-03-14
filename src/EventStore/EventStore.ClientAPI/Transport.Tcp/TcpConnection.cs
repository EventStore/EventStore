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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using EventStore.ClientAPI.Common.Utils;

namespace EventStore.ClientAPI.Transport.Tcp
{
    internal class TcpConnection : TcpConnectionBase, ITcpConnection
    {
        private const int BufferSize = 512;
        private const int MaxSendPacketSize = 64 * 1024;

        private static readonly SocketArgsPool SocketArgsPool = new SocketArgsPool("TcpConnection.SocketArgsPool", 
                                                                                   TcpConfiguration.SendReceivePoolSize, 
                                                                                   () => new SocketAsyncEventArgs());
        internal static TcpConnection CreateConnectingTcpConnection(Guid connectionId, 
                                                                    IPEndPoint remoteEndPoint,
                                                                    TcpClientConnector connector,
                                                                    Action<TcpConnection> onConnectionEstablished,
                                                                    Action<TcpConnection, SocketError> onConnectionFailed,
                                                                    Action<TcpConnection, SocketError> onConnectionClosed)
        {
            var connection = new TcpConnection(connectionId, remoteEndPoint, onConnectionClosed);
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
                                  });
            return connection;
        }

        internal static TcpConnection CreateAcceptedTcpConnection(Guid connectionId, 
                                                                  IPEndPoint effectiveEndPoint, 
                                                                  Socket socket, 
                                                                  Action<TcpConnection, SocketError> onConnectionClosed)
        {
            var connection = new TcpConnection(connectionId, effectiveEndPoint, onConnectionClosed);
            connection.InitSocket(socket);
            return connection;
        }

        public readonly Guid ConnectionId;
        public IPEndPoint EffectiveEndPoint { get; private set; }
        public int SendQueueSize { get { return _sendQueue.Count; } }

        private Socket _socket;
        private SocketAsyncEventArgs _receiveSocketArgs;
        private SocketAsyncEventArgs _sendSocketArgs;

        private readonly Common.Concurrent.ConcurrentQueue<ArraySegment<byte>> _sendQueue = new Common.Concurrent.ConcurrentQueue<ArraySegment<byte>>();
        private readonly Queue<Tuple<ArraySegment<byte>, Action>> _receiveQueue = new Queue<Tuple<ArraySegment<byte>, Action>>();
        private readonly MemoryStream _memoryStream = new MemoryStream();

        private readonly object _receivingLock = new object();
        private readonly object _sendingLock = new object();
        private bool _isSending;
        private int _closed;

        private Action<ITcpConnection, IEnumerable<ArraySegment<byte>>> _receiveCallback;
        private readonly Action<TcpConnection, SocketError> _onConnectionClosed;

        private int _packagesSent;
        private long _bytesSent;
        private int _packagesReceived;
        private long _bytesReceived;
        private int _sentAsyncs;
        private int _sentAsyncCallbacks;
        private int _recvAsyncs;
        private int _recvAsyncCallbacks;

        private TcpConnection(Guid connectionId, IPEndPoint effectiveEndPoint, Action<TcpConnection, SocketError> onConnectionClosed)
        {
            Ensure.NotEmptyGuid(connectionId, "connectionId");
            Ensure.NotNull(effectiveEndPoint, "effectiveEndPoint");

            ConnectionId = connectionId;
            EffectiveEndPoint = effectiveEndPoint;
            _onConnectionClosed = onConnectionClosed;
        }

        private void InitSocket(Socket socket)
        {
            //Console.WriteLine("TcpConnection::InitSocket[{0}]", socket.RemoteEndPoint);

            base.InitSocket(socket, EffectiveEndPoint);
            lock (_sendingLock)
            {
                _socket = socket;
                try
                {
                    socket.NoDelay = true;
                    //socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, TcpConfiguration.SocketBufferSize);
                    //socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, TcpConfiguration.SocketBufferSize);
                }
                catch (ObjectDisposedException)
                {
                    CloseInternal(SocketError.Shutdown);
                    _socket = null;
                    return;
                }

                _receiveSocketArgs = SocketArgsPool.Get();
                _receiveSocketArgs.AcceptSocket = socket;
                _receiveSocketArgs.Completed += OnReceiveAsyncCompleted;

                _sendSocketArgs = SocketArgsPool.Get();
                _sendSocketArgs.AcceptSocket = socket;
                _sendSocketArgs.Completed += OnSendAsyncCompleted;
            }
            StartReceive();
            TrySend();
        }

        public void EnqueueSend(IEnumerable<ArraySegment<byte>> data)
        {
            lock (_sendingLock)
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
            lock (_sendingLock)
            {
                if (_isSending || _sendQueue.Count == 0 || _socket == null)
                    return;

                if (TcpConnectionMonitor.Default.IsSendBlocked())
                    return;
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

            Interlocked.Add(ref _bytesSent, _memoryStream.Length);
            _sendSocketArgs.SetBuffer(_memoryStream.GetBuffer(), 0, (int) _memoryStream.Length);

            if (_sendSocketArgs.Count == 0)
            {
                lock (_sendingLock)
                {
                    _isSending = false;
                    return;
                }
            }

            try
            {
                Interlocked.Increment(ref _sentAsyncs);
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
            Interlocked.Increment(ref _sentAsyncCallbacks);
            if (socketArgs.SocketError != SocketError.Success)
            {
                NotifySendCompleted(0);
                ReturnSendingSocketArgs();
                CloseInternal(socketArgs.SocketError);
                return;
            }

            NotifySendCompleted(socketArgs.Count);
            Interlocked.Increment(ref _packagesSent);

            lock (_sendingLock)
            {
                _isSending = false;
            }
            TrySend();
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
            var buffer = new ArraySegment<byte>(new byte[BufferSize]);

            //TODO AN: do we need to lock on _receiveSocketArgs?..
            lock (_receiveSocketArgs)
            {
                _receiveSocketArgs.SetBuffer(buffer.Array, buffer.Offset, buffer.Count);
                if (_receiveSocketArgs.Buffer == null)
                    throw new Exception("Buffer was not set");
            }
            try
            {
                Interlocked.Increment(ref _recvAsyncs);
                NotifyReceiveStarting();
                bool firedAsync;
                lock (_receiveSocketArgs)
                {
                    if (_receiveSocketArgs.Buffer == null)
                        throw new Exception("Buffer was lost");
                    firedAsync = _receiveSocketArgs.AcceptSocket.ReceiveAsync(_receiveSocketArgs);
                }
                if (!firedAsync)
                {
                    ProcessReceive(_receiveSocketArgs);
                }
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
            if (socketArgs != _receiveSocketArgs)
                throw new Exception("Invalid socket args received");
            Interlocked.Increment(ref _recvAsyncCallbacks);

            // socket closed normally or some error occurred
            if (socketArgs.BytesTransferred == 0 || socketArgs.SocketError != SocketError.Success)
            {
                NotifyReceiveCompleted(0);
                ReturnReceivingSocketArgs();
                CloseInternal(socketArgs.SocketError);
                return;
            }

            NotifyReceiveCompleted(socketArgs.BytesTransferred);
            Interlocked.Increment(ref _packagesReceived);
            Interlocked.Add(ref _bytesReceived, socketArgs.BytesTransferred);

            var receiveBufferSegment = new ArraySegment<byte>(socketArgs.Buffer, socketArgs.Offset, socketArgs.BytesTransferred);
            Action disposeBufferAction = () => { };
            lock (_receivingLock)
            {
                _receiveQueue.Enqueue(Tuple.Create(receiveBufferSegment, disposeBufferAction));
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
            List<Tuple<ArraySegment<byte>, Action>> res;

            lock (_receivingLock)
            {
                // no awaiting callback or no data to dequeue
                if (_receiveCallback == null || _receiveQueue.Count == 0)
                    return;

                res = new List<Tuple<ArraySegment<byte>, Action>>(_receiveQueue.Count);
                while (_receiveQueue.Count > 0)
                {
                    var arraySegments = _receiveQueue.Dequeue();
                    res.Add(arraySegments);
                }

                callback = _receiveCallback;
                _receiveCallback = null;
            }
            callback(this, res.Select(v => v.Item1).ToArray());
            int bytes = 0;
            foreach (var tuple in res)
            {
                bytes += tuple.Item1.Count;
                tuple.Item2(); // dispose buffers
            }
            NotifyReceiveDispatched(bytes);
        }

        public void Close()
        {
            CloseInternal(SocketError.Success); // normal socket closing
        }

        private void CloseInternal(SocketError socketError)
        {
            var isClosed = Interlocked.CompareExchange(ref _closed, 1, 0) != 0;
            if (isClosed)
                return;

            NotifyClosed();

            Console.WriteLine("[{0:HH:mm:ss.fff}: {1}]:\nConnection ID: {2:B}\nReceived packages: {3}, bytes: {4}\nSent packages: {5}, bytes: {6}\n"
                              + "SendAsync calls: {7}, callbacks: {8}\nReceiveAsync calls: {9}, callbacks: {10}\n",
                              DateTime.UtcNow,
                              EffectiveEndPoint,
                              ConnectionId,
                              _packagesReceived,
                              _bytesReceived,
                              _packagesSent,
                              _bytesSent,
                              _sentAsyncs,
                              _sentAsyncCallbacks,
                              _recvAsyncs,
                              _recvAsyncCallbacks);

            if (_socket != null)
            {
                Helper.EatException(() => _socket.Shutdown(SocketShutdown.Both));
                Helper.EatException(() => _socket.Close(TcpConfiguration.SocketCloseTimeoutMs));
                _socket = null;
            }

            lock (_sendingLock)
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
            return EffectiveEndPoint.ToString();
        }
    }
}