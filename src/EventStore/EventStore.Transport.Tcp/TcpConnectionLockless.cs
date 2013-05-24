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
using EventStore.BufferManagement;
using EventStore.Common.Log;
using EventStore.Common.Utils;

namespace EventStore.Transport.Tcp
{
    public class TcpConnectionLockless : TcpConnectionBase, ITcpConnection
    {
        internal const int MaxSendPacketSize = 64 * 1024;
        internal static readonly BufferManager BufferManager = new BufferManager(TcpConfiguration.BufferChunksCount, TcpConfiguration.SocketBufferSize);

        private static readonly ILogger Log = LogManager.GetLoggerFor<TcpConnection>();
        private static readonly SocketArgsPool SocketArgsPool = new SocketArgsPool("TcpConnection.SocketArgsPool", 
                                                                                   TcpConfiguration.SendReceivePoolSize, 
                                                                                   () => new SocketAsyncEventArgs());

        public static ITcpConnection CreateConnectingTcpConnection(Guid connectionId, 
                                                                   IPEndPoint remoteEndPoint, 
                                                                   TcpClientConnector connector, 
                                                                   Action<ITcpConnection> onConnectionEstablished, 
                                                                   Action<ITcpConnection, SocketError> onConnectionFailed,
                                                                   bool verbose)
        {
            var connection = new TcpConnectionLockless(connectionId, remoteEndPoint, verbose);
// ReSharper disable ImplicitlyCapturedClosure
            connector.InitConnect(remoteEndPoint,
                                  (_, socket) =>
                                  {
                                      if (connection.InitSocket(socket))
                                      {
                                          if (onConnectionEstablished != null)
                                              onConnectionEstablished(connection);
                                          connection.StartReceive();
                                          connection.TrySend();
                                      }
                                  },
                                  (_, socketError) =>
                                  {
                                      if (onConnectionFailed != null)
                                          onConnectionFailed(connection, socketError);
                                  });
// ReSharper restore ImplicitlyCapturedClosure
            return connection;
        }

        public static ITcpConnection CreateAcceptedTcpConnection(Guid connectionId, IPEndPoint effectiveEndPoint, Socket socket, bool verbose)
        {
            var connection = new TcpConnectionLockless(connectionId, effectiveEndPoint, verbose);
            connection.InitSocket(socket);
            connection.StartReceive();
            connection.TrySend();
            return connection;
        }

        public event Action<ITcpConnection, SocketError> ConnectionClosed;
        public Guid ConnectionId { get { return _connectionId; } }
        public IPEndPoint EffectiveEndPoint { get { return _effectiveEndPoint; } }
        public int SendQueueSize { get { return _sendQueue.Count; } }

        private readonly Guid _connectionId;
        private readonly IPEndPoint _effectiveEndPoint;
        private readonly bool _verbose;

        private Socket _socket;
        private SocketAsyncEventArgs _receiveSocketArgs;
        private SocketAsyncEventArgs _sendSocketArgs;

        private readonly Common.Concurrent.ConcurrentQueue<ArraySegment<byte>> _sendQueue = new Common.Concurrent.ConcurrentQueue<ArraySegment<byte>>();
        private readonly Common.Concurrent.ConcurrentQueue<Tuple<ArraySegment<byte>, int>> _receiveQueue = new Common.Concurrent.ConcurrentQueue<Tuple<ArraySegment<byte>, int>>();
        private readonly MemoryStream _memoryStream = new MemoryStream();

        private int _sending;
        private int _receiving;
        private volatile int _closed;

        private Action<ITcpConnection, IEnumerable<ArraySegment<byte>>> _receiveCallback;

        private TcpConnectionLockless(Guid connectionId, IPEndPoint effectiveEndPoint, bool verbose)
        {
            Ensure.NotEmptyGuid(connectionId, "connectionId");
            Ensure.NotNull(effectiveEndPoint, "effectiveEndPoint");

            _connectionId = connectionId;
            _effectiveEndPoint = effectiveEndPoint;
            _verbose = verbose;
        }

        private bool InitSocket(Socket socket)
        {
            InitSocket(socket, _effectiveEndPoint);

            try
            {
                socket.NoDelay = true;
            }
            catch (ObjectDisposedException)
            {
                CloseInternal(SocketError.Shutdown, "Socket disposed.");
                return false;
            }

            var receiveSocketArgs = SocketArgsPool.Get();
            receiveSocketArgs.AcceptSocket = socket;
            receiveSocketArgs.Completed += OnReceiveAsyncCompleted;

            var sendSocketArgs = SocketArgsPool.Get();
            sendSocketArgs.AcceptSocket = socket;
            sendSocketArgs.Completed += OnSendAsyncCompleted;

            Interlocked.Exchange(ref _socket, socket);
            Interlocked.Exchange(ref _receiveSocketArgs, receiveSocketArgs);
            Interlocked.Exchange(ref _sendSocketArgs, sendSocketArgs);

            if (_closed != 0)
            {
                CloseSocket();
                ReturnReceivingSocketArgs();
                if (Interlocked.CompareExchange(ref _sending, 1, 0) == 0)
                    ReturnSendingSocketArgs();
                return false;
            }

            return true;
        }

        public void EnqueueSend(IEnumerable<ArraySegment<byte>> data)
        {
            using (var memStream = new MemoryStream())
            {
                int bytes = 0;
                foreach (var segment in data)
                {
                    memStream.Write(segment.Array, segment.Offset, segment.Count);
                    bytes += segment.Count;
                }
                _sendQueue.Enqueue(new ArraySegment<byte>(memStream.GetBuffer(), 0, (int)memStream.Length));
                NotifySendScheduled(bytes);
            }

            TrySend();
        }

        private void TrySend()
        {
            if (Interlocked.CompareExchange(ref _sending, 1, 0) != 0)
                return;

            do
            {
                if (_sendQueue.Count > 0 && _sendSocketArgs != null)
                {
                    //if (TcpConnectionMonitor.Default.IsSendBlocked()) return;

                    _memoryStream.SetLength(0);

                    ArraySegment<byte> sendPiece;
                    while (_sendQueue.TryDequeue(out sendPiece))
                    {
                        _memoryStream.Write(sendPiece.Array, sendPiece.Offset, sendPiece.Count);
                        if (_memoryStream.Length >= MaxSendPacketSize)
                            break;
                    }

                    _sendSocketArgs.SetBuffer(_memoryStream.GetBuffer(), 0, (int)_memoryStream.Length);

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
                    return;
                }
                Interlocked.Exchange(ref _sending, 0);
                if (_closed != 0)
                {
                    if (Interlocked.CompareExchange(ref _sending, 1, 0) == 0)
                        ReturnSendingSocketArgs();
                    return;
                }
            } while (_sendQueue.Count > 0 && _sendSocketArgs != null && Interlocked.CompareExchange(ref _sending, 1, 0) == 0);
        }

        private void OnSendAsyncCompleted(object sender, SocketAsyncEventArgs e)
        {
            // No other code should go here. All handling is the same for sync/async completion.
            ProcessSend(e);
        }

        private void ProcessSend(SocketAsyncEventArgs socketArgs)
        {
            if (socketArgs != _sendSocketArgs)
            {
                Log.Fatal("Wrong send socket args.");
                throw new Exception("Wrong send socket args.");
            }

            if (socketArgs.SocketError != SocketError.Success)
            {
                NotifySendCompleted(0);
                ReturnSendingSocketArgs();
                CloseInternal(socketArgs.SocketError, "Socket send error.");
            }
            else
            {
                NotifySendCompleted(socketArgs.Count);
                Interlocked.Exchange(ref _sending, 0);
                if (_closed != 0)
                {
                    if (Interlocked.CompareExchange(ref _sending, 1, 0) == 0)
                        ReturnSendingSocketArgs();
                    return;
                }
                TrySend();
            }
        }

        public void ReceiveAsync(Action<ITcpConnection, IEnumerable<ArraySegment<byte>>> callback)
        {
            if (callback == null)
                throw new ArgumentNullException("callback");

            if (Interlocked.Exchange(ref _receiveCallback, callback) != null)
            {
                Log.Fatal("ReceiveAsync called again while previous call wasn't fulfilled");
                throw new InvalidOperationException("ReceiveAsync called again while previous call wasn't fulfilled");
            }
            TryDequeueReceivedData();
        }

        private void StartReceive()
        {
            var buffer = BufferManager.CheckOut();
            if (buffer.Array == null || buffer.Count == 0 || buffer.Array.Length < buffer.Offset + buffer.Count)
                throw new Exception("Invalid buffer allocated");

            try
            {
                NotifyReceiveStarting();
                _receiveSocketArgs.SetBuffer(buffer.Array, buffer.Offset, buffer.Count);
                var firedAsync = _receiveSocketArgs.AcceptSocket.ReceiveAsync(_receiveSocketArgs);
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
            if (socketArgs != _receiveSocketArgs)
            {
                Log.Fatal("Wrong receive socket args.");
                throw new Exception("Wrong receive socket args.");
            }

            // socket closed normally or some error occurred
            if (socketArgs.BytesTransferred == 0 || socketArgs.SocketError != SocketError.Success)
            {
                NotifyReceiveCompleted(0);
                ReturnReceivingSocketArgs();
                CloseInternal(socketArgs.SocketError, socketArgs.SocketError != SocketError.Success ? "Socket receive error" : "Socket closed");
                return;
            }
            
            NotifyReceiveCompleted(socketArgs.BytesTransferred);
            
            var dataBuffer = new ArraySegment<byte>(socketArgs.Buffer, socketArgs.Offset, socketArgs.BytesTransferred);
            _receiveQueue.Enqueue(Tuple.Create(dataBuffer, socketArgs.Count));
            socketArgs.SetBuffer(null, 0, 0);

            StartReceive();
            TryDequeueReceivedData();
        }

        private void TryDequeueReceivedData()
        {
            if (Interlocked.CompareExchange(ref _receiving, 1, 0) != 0)
                return;
            do
            {
                if (_receiveQueue.Count > 0 && _receiveCallback != null)
                {
                    var callback = Interlocked.Exchange(ref _receiveCallback, null);
                    if (callback == null)
                    {
                        Log.Fatal("Some threading issue in TryDequeueReceivedData! Callback is null!");
                        throw new Exception("Some threading issue in TryDequeueReceivedData! Callback is null!");
                    }

                    var dequeueResultList = new List<Tuple<ArraySegment<byte>, int>>(_receiveQueue.Count);
                    Tuple<ArraySegment<byte>, int> piece;
                    while (_receiveQueue.TryDequeue(out piece))
                    {
                        dequeueResultList.Add(piece);
                    }

                    callback(this, dequeueResultList.Select(v => v.Item1));

                    int bytes = 0;
                    for (int i = 0, n = dequeueResultList.Count; i < n; ++i)
                    {
                        var tuple = dequeueResultList[i];
                        bytes += tuple.Item1.Count;
                        BufferManager.CheckIn(new ArraySegment<byte>(tuple.Item1.Array, tuple.Item1.Offset, tuple.Item2));
                    }
                    NotifyReceiveDispatched(bytes);
                }
                Interlocked.Exchange(ref _receiving, 0);
            } while (_receiveQueue.Count > 0
                     && _receiveCallback != null
                     && Interlocked.CompareExchange(ref _receiving, 1, 0) == 0);
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
            if (_verbose)
            {
                Log.Info("[{0:HH:mm:ss.fff}: {1}]:\nConnection ID: {2:B}\nReceived bytes: {3}, Sent bytes: {4}\n"
                         + "Send calls: {5}, callbacks: {6}\nReceive calls: {7}, callbacks: {8}\nClose reason: [{9}] {10}\n",
                         DateTime.UtcNow, EffectiveEndPoint, _connectionId,
                         TotalBytesReceived, TotalBytesSent,
                         SendCalls, SendCallbacks,
                         ReceiveCalls, ReceiveCallbacks,
                         socketError, reason);
            }
            CloseSocket();
            if (Interlocked.CompareExchange(ref _sending, 1, 0) == 0)
                ReturnSendingSocketArgs();
            var handler = ConnectionClosed;
            if (handler != null)
                handler(this, socketError);
        }

        private void CloseSocket()
        {
            var socket = Interlocked.Exchange(ref _socket, null);
            if (socket != null)
            {
                Helper.EatException(() => socket.Shutdown(SocketShutdown.Both));
                Helper.EatException(() => socket.Close(TcpConfiguration.SocketCloseTimeoutMs));
            }
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
                {
                    BufferManager.CheckIn(new ArraySegment<byte>(socketArgs.Buffer, socketArgs.Offset, socketArgs.Count));
                    socketArgs.SetBuffer(null, 0, 0);
                }
                SocketArgsPool.Return(socketArgs);
            }
        }
        
        public override string ToString()
        {
            return EffectiveEndPoint.ToString();
        }
    }
}