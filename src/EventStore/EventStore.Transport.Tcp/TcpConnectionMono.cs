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

namespace EventStore.Transport.Tcp
{
    public static class BeginEnd {

    public class TcpConnection: TcpConnectionBase, ITcpConnection
    {
        internal static TcpConnection CreateConnectingTcpConnection(IPEndPoint remoteEndPoint, TcpClientConnector connector, Action<TcpConnection> onConnectionEstablished, Action<TcpConnection, SocketError> onConnectionFailed)
        {
            var connection = new TcpConnection(remoteEndPoint);
            connector.InitConnect(remoteEndPoint,
                                  (_, socket) =>
                                  {
                                      connection.InitSocket(socket);
                                      if (onConnectionEstablished != null)
                                          onConnectionEstablished(connection);
                                      connection.TrySend();
                                  },
                                  (_, socketError) =>
                                  {
                                      if (onConnectionFailed != null)
                                          onConnectionFailed(connection, socketError);
                                  });
            return connection;
        }

        internal static TcpConnection CreateAcceptedTcpConnection(IPEndPoint effectiveEndPoint, Socket socket)
        {
            var connection = new TcpConnection(effectiveEndPoint);
            connection.InitSocket(socket);
            return connection;
        }
        private const int MaxSendPacketSize = 64 * 1024;

        private static readonly BufferManager BufferManager =
            new BufferManager(TcpConfiguration.BufferChunksCount, TcpConfiguration.SocketBufferSize);

        private int _packagesSent;
        private long _bytesSent;
        private int _packagesReceived;
        private long _bytesReceived;

        public event Action<ITcpConnection, SocketError> ConnectionClosed;

        public IPEndPoint EffectiveEndPoint { get; private set; }
        public int SendQueueSize { get { return _sendQueue.Count; } }

        private Socket _socket;

#if __MonoCS__
        private readonly Common.ConcurrentCollections.ConcurrentQueue<ArraySegment<byte>> _sendQueue = new Common.ConcurrentCollections.ConcurrentQueue<ArraySegment<byte>>();
#else
        private readonly System.Collections.Concurrent.ConcurrentQueue<ArraySegment<byte>> _sendQueue = new System.Collections.Concurrent.ConcurrentQueue<ArraySegment<byte>>();
#endif

        private readonly Queue<IEnumerable<ArraySegment<byte>>> _receiveQueue = new Queue<IEnumerable<ArraySegment<byte>>>();

        private readonly MemoryStream _memoryStream = new MemoryStream();

        private readonly object _receivingLock = new object();
        private readonly object _sendingLock = new object();
        private bool _isSending;
        private int _closed;

        private Action<TcpConnection, IEnumerable<ArraySegment<byte>>> _receiveCallback;

        private int _sentAsyncs;
        private int _sentAsyncCallbacks;
        private int _recvAsyncs;
        private int _recvAsyncCallbacks;

        private ArraySegment<byte> _currentBuffer;

        private TcpConnection(IPEndPoint effectiveEndPoint)
        {
            if (effectiveEndPoint == null)
                throw new ArgumentNullException("effectiveEndPoint");

            EffectiveEndPoint = effectiveEndPoint;
        }

        private void InitSocket(Socket socket)
        {
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
            }
            StartReceive();
        }

        public void EnqueueSend(IEnumerable<ArraySegment<byte>> data)
        {
            lock (_sendingLock)
            {
                foreach (var segment in data)
                {
                    _sendQueue.Enqueue(segment);
                }
            }

            TrySend();
        }

        private void TrySend()
        {
            lock (_sendingLock)
            {
                if (_isSending || _sendQueue.Count == 0 || _socket == null)
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

            try
            {
                Interlocked.Increment(ref _sentAsyncs);

                //Console.WriteLine(String.Format("{0:mmss.fff}", DateTime.UtcNow) + " sending " + _memoryStream.Length + " bytes.");

                var buf = _memoryStream.GetBuffer();
                _socket.BeginSend(buf, 0, (int) _memoryStream.Length, SocketFlags.None, OnSendAsyncCompleted, null);
            }
            catch (SocketException exc)
            {
                CloseInternal(exc.SocketErrorCode);
            }
            catch (Exception)
            {
                CloseInternal(SocketError.Shutdown);
            }
        }

        private void OnSendAsyncCompleted(IAsyncResult ar)
        {
            Interlocked.Increment(ref _sentAsyncCallbacks);
            try
            {
                var sentPackages = Interlocked.Increment(ref _packagesSent);
                //Console.WriteLine(String.Format("{0:mmss.fff}", DateTime.UtcNow) + " done sending " + _memoryStream.Length + " bytes.");

                var sendBytes = _socket.EndSend(ar);
                Interlocked.Add(ref _bytesSent, sendBytes);

                lock (_sendingLock)
                {
                    _isSending = false;
                }
                TrySend();
            }
            catch (SocketException exc)
            {
                CloseInternal(exc.SocketErrorCode);
            }
            catch (Exception)
            {
                CloseInternal(SocketError.Shutdown);
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
            try
            {
                Interlocked.Increment(ref _recvAsyncs);
                
                _currentBuffer = BufferManager.CheckOut();
                _socket.BeginReceive(_currentBuffer.Array,
                                     _currentBuffer.Offset,
                                     _currentBuffer.Count,
                                     SocketFlags.None,
                                     OnReceiveAsyncCompleted,
                                     null);
            }
            catch (SocketException exc)
            {
                CloseInternal(exc.SocketErrorCode);
            }
            catch (Exception)
            {
                CloseInternal(SocketError.Shutdown);
            }
        }

        private void OnReceiveAsyncCompleted(IAsyncResult ar)
        {
            Interlocked.Increment(ref _recvAsyncCallbacks);

            int receivedBytes;
            try
            {
                receivedBytes = _socket.EndReceive(ar);
            }
            catch (SocketException exc)
            {
                BufferManager.CheckIn(_currentBuffer);
                CloseInternal(exc.SocketErrorCode);
                return;
            }
            catch (Exception)
            {
                BufferManager.CheckIn(_currentBuffer);
                CloseInternal(SocketError.Shutdown);
                return;
            }

            var receivedPackages = Interlocked.Increment(ref _packagesReceived);
            var totalReceivedBytes = Interlocked.Add(ref _bytesReceived, receivedBytes);

            if (receivedBytes == 0) // correct socket closing 
            {
                BufferManager.CheckIn(_currentBuffer);
                CloseInternal(SocketError.Success);
                return;
            }

            //Console.WriteLine(String.Format("{0:mmss.fff}", DateTime.UtcNow) + " received " + receivedBytes + " bytes.");

            // OK, so what does this line of code do? It makes an ArraySegment<byte> representing the data 
            // that we actually read.
            // Then it constructs a little array to meet the IEnumerable interface.
            // Then it makes original buffer (ArraySegment<byte>) we used for receive operation.
            // Then it builds an IEnumerable that will dispose of our buffer (returning it to the buffer pool) 
            // later (as in later when some other thread (but it may be on this thread, we aren't sure) processes 
            // this buffer).
            // This should be benchmarked vs copying the byte array every time into a new byte array
            var receiveBufferSegment = new[]
            {
                new ArraySegment<byte>(_currentBuffer.Array, _currentBuffer.Offset, receivedBytes)
            };

            lock (_receivingLock)
            {
                _receiveQueue.Enqueue(receiveBufferSegment.CallAfterEvaluation(
                    x => BufferManager.CheckIn(x), _currentBuffer));
            }

            StartReceive();
            TryDequeueReceivedData();
        }

        private void TryDequeueReceivedData()
        {
            Action<TcpConnection, IEnumerable<ArraySegment<byte>>> callback;
            List<IEnumerable<ArraySegment<byte>>> res = null;

            lock (_receivingLock)
            {
                // no awaiting callback or no data to dequeue
                if (_receiveCallback == null || _receiveQueue.Count == 0)
                    return;

                res = new List<IEnumerable<ArraySegment<byte>>>(_receiveQueue.Count);
                while (_receiveQueue.Count > 0)
                {
                    res.Add(_receiveQueue.Dequeue());
                }

                callback = _receiveCallback;
                _receiveCallback = null;
            }
            callback(this, res.SelectMany(x => x));
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

            Console.WriteLine("[{0}]:\nReceived packages: {1}, bytes: {2}\nSend packages: {3}, bytes: {4}\nSendAsync calls: {5}, callbacks: {6}\nReceiveAsync calls: {7}, callbacks: {8}\n",
                              EffectiveEndPoint,
                              _packagesReceived,
                              _bytesReceived,
                              _packagesSent,
                              _bytesSent,
                              _sentAsyncs,
                              _sentAsyncCallbacks,
                              _recvAsyncs,
                              _recvAsyncCallbacks);

            Helper.EatException(() => _socket.Shutdown(SocketShutdown.Both));
            Helper.EatException(() => _socket.Close(TcpConfiguration.SocketCloseTimeoutMs));
            _socket = null;

            var handler = ConnectionClosed;
            if (handler != null)
                handler(this, socketError);
        }
    }
    
    }
}