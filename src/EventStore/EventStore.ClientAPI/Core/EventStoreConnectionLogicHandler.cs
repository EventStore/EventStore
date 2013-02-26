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
using System.Diagnostics;
using System.Threading;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Transport.Tcp;

namespace EventStore.ClientAPI.Core
{
    internal class EventStoreConnectionLogicHandler
    {
        private readonly Common.Concurrent.ConcurrentQueue<Message> _messageQueue = new Common.Concurrent.ConcurrentQueue<Message>();
        private readonly Dictionary<Type, Action<Message>> _handlers = new Dictionary<Type, Action<Message>>();
        private int _isProcessing;

        private readonly TcpConnector _connector = new TcpConnector();
        private TcpTypedConnection _connection;

        private bool _connectionActive;
        private bool _stopping;

        private readonly DateTime _startTime = DateTime.UtcNow;
        private readonly Stopwatch _watch = Stopwatch.StartNew();
        private DateTime _lastReconnectionTimestamp;



        public EventStoreConnectionLogicHandler()
        {
            RegisterHandler<EstablishTcpConnectionMessage>(EstablishTcpConnection);   
        }

        private void RegisterHandler<T>(Action<T> handler) where T : Message
        {
            Ensure.NotNull(handler, "handler");
            _handlers.Add(typeof(T), msg => handler((T)msg));
        }

        private DateTime GetNow()
        {
            return _startTime + _watch.Elapsed;
        }

        public void EnqueueMessage(Message message)
        {
            Ensure.NotNull(message, "message");

            _messageQueue.Enqueue(message);
            if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) == 0)
                ThreadPool.QueueUserWorkItem(ProcessQueue);
        }

        private void ProcessQueue(object state)
        {
            do
            {
                Message message;

                while (_messageQueue.TryDequeue(out message))
                {
                    Action<Message> handler;
                    if (!_handlers.TryGetValue(message.GetType(), out handler))
                        throw new Exception(string.Format("No handler registered for message {0}", message.GetType().Name));
                    handler(message);
                }

                Interlocked.Exchange(ref _isProcessing, 0);
            } while (_messageQueue.Count > 0 && Interlocked.CompareExchange(ref _isProcessing, 1, 0) == 0);
        }

        private void EstablishTcpConnection(EstablishTcpConnectionMessage message)
        {
            if (_connectionActive) throw new InvalidOperationException("EventStoreConnection is already active.");
            if (_stopping) throw new InvalidOperationException("EventStoreConnection has been closed.");
            
            _connectionActive = true;
            _lastReconnectionTimestamp = GetNow();
            _connection = _connector.CreateTcpConnection(message.EndPoint, OnPackageReceived, OnConnectionEstablished, OnConnectionClosed);
            
//            _timeoutCheckStopwatch.Start();

//            _worker = new Thread(MainLoop) { IsBackground = true, Name = "EventStoreConnection worker" };
//            _worker.Start();

            message.Task.SetResult(null);
        }

    }
}
