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
using EventStore.ClientAPI.Transport.Tcp;
using Ensure = EventStore.ClientAPI.Common.Utils.Ensure;

namespace EventStore.ClientAPI.Tcp
{
    class TcpConnectionBase : IMonitoredTcpConnection
    {
        private Socket _socket;
        private IPEndPoint _endPoint;

        // this lock is per connection, so unlilkely to block on it
        // so locking any acces without any optimization
        private readonly object _lock = new object();

        private DateTime? _lastSendStarted;
        private DateTime? _lastReceiveStarted;
        private bool _isClosed;

        private uint _pendingSendBytes;
        private uint _inSendBytes;
        private uint _pendingReceivedBytes;
        private long _totaBytesSent;
        private long _totaBytesReceived;

        public TcpConnectionBase()
        {
            TcpConnectionMonitor.Default.Register(this);
        }

        public IPEndPoint EndPoint
        {
            get
            {
                lock (_lock)
                {
                    return _endPoint;
                }
            }
        }

        public bool IsReadyForSend
        {
            get
            {
                try
                {
                    return !_isClosed && _socket.Poll(0, SelectMode.SelectWrite);
                }
                catch (ObjectDisposedException)
                {
                    //TODO: why do we get this?
                    return false;
                }
            }
        }



        public bool IsReadyForReceive
        {
            get
            {
                try
                {
                    return !_isClosed && _socket.Poll(0, SelectMode.SelectRead);
                }
                catch (ObjectDisposedException)
                {
                    //TODO: why do we get this?
                    return false;
                }
            }
        }

        public bool IsInitialized
        {
            get
            {
                lock (_lock)
                {
                    return _socket != null;
                }
            }
        }

        public bool IsFaulted
        {
            get
            {
                try
                {
                    return !_isClosed && _socket.Poll(0, SelectMode.SelectError);
                }
                catch (ObjectDisposedException)
                {
                    //TODO: why do we get this?
                    return false;
                }
            }
        }

        public bool IsClosed
        {
            get
            {
                lock (_lock)
                {
                    return _isClosed;
                }
            }
        }

        public bool InSend
        {
            get
            {
                lock (_lock)
                {
                    return _lastSendStarted != null;
                }
            }
        }

        public bool InReceive
        {
            get
            {
                lock (_lock)
                {
                    return _lastReceiveStarted != null;
                }
            }
        }

        public DateTime? LastSendStarted
        {
            get
            {
                lock (_lock)
                {
                    return _lastSendStarted;
                }
            }
        }

        public DateTime? LastReceiveStarted
        {
            get
            {
                lock (_lock)
                {
                    return _lastReceiveStarted;
                }
            }
        }

        public uint PendingSendBytes
        {
            get
            {
                lock (_lock)
                {
                    return _pendingSendBytes;
                }
            }
        }

        public uint InSendBytes
        {
            get
            {
                lock (_lock)
                {
                    return _inSendBytes;
                }
            }
        }

        public uint PendingReceivedBytes
        {
            get
            {
                lock (_lock)
                {
                    return _pendingReceivedBytes;
                }
            }
        }

        public long TotalBytesSent
        {
            get
            {
                lock (_lock)
                {
                    return _totaBytesSent;
                }
            }
        }

        public long TotalBytesReceived
        {
            get
            {
                lock (_lock)
                {
                    return _totaBytesReceived;
                }
            }
        }

        protected void InitSocket(Socket socket, IPEndPoint endPoint)
        {
            Ensure.NotNull(socket, "socket");
            Ensure.NotNull(endPoint, "endPoint");

            _socket = socket;
            _endPoint = endPoint;
        }

        protected void NotifySendScheduled(uint bytes)
        {
            lock (_lock)
            {
                _pendingSendBytes += bytes;
            }
        }

        protected void NotifySendStarting(uint bytes)
        {
            lock (_lock)
            {
                if (_lastSendStarted != null)
                    throw new Exception("Concurrent send deteced");
                _lastSendStarted = DateTime.UtcNow;
                _pendingSendBytes -= bytes;
                _inSendBytes += bytes;
            }
        }

        protected void NotifySendCompleted(uint bytes)
        {
            lock (_lock)
            {
                _lastSendStarted = null;
                _inSendBytes -= bytes;
                _totaBytesSent += bytes;
            }
        }

        protected void NotifyReceiveStarting()
        {
            lock (_lock)
            {
                if (_lastReceiveStarted != null)
                    throw new Exception("Concurrent receive deteced");
                _lastReceiveStarted = DateTime.UtcNow;
            }
        }

        protected void NotifyReceiveCompleted(uint bytes)
        {
            lock (_lock)
            {
                _lastReceiveStarted = null;
                _pendingReceivedBytes += bytes;
                _totaBytesReceived += bytes;
            }
        }

        protected void NotifyReceiveDispatched(uint bytes)
        {
            lock (_lock)
            {
                _pendingReceivedBytes -= bytes;
            }
        }

        protected void NotifyClosed()
        {
            lock (_lock)
            {
                _isClosed = true;
            }
            TcpConnectionMonitor.Default.Unregister(this);
        }
    }
}