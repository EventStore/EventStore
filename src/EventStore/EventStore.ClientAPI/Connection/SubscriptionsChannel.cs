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
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI.Common.Log;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.Messages;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.Transport.Tcp;

namespace EventStore.ClientAPI.Connection
{
    internal class SubscriptionsChannel
    {
        private const int ConnectionTimeoutMs = 2000;

        private readonly ILogger _log;

        private readonly TcpConnector _connector;
        private TcpTypedConnection _connection;

        private readonly ManualResetEvent _connectedEvent = new ManualResetEvent(false);
        private readonly object _subscriptionChannelLock = new object();

        private Thread _executionThread;
        private volatile bool _stopExecutionThread;
        private readonly ConcurrentQueue<Action> _executionQueue = new ConcurrentQueue<Action>(); 

        private readonly ConcurrentDictionary<Guid, Subscription> _subscriptions = new ConcurrentDictionary<Guid, Subscription>();

        public SubscriptionsChannel(TcpConnector connector)
        {
            _connector = connector;
            _log = LogManager.GetLogger();
        }

        public void Close(bool stopBackgroundThread = true)
        {
            lock (_subscriptionChannelLock)
            {
                if (_connection != null)
                    _connection.Close();

                _stopExecutionThread = stopBackgroundThread;
            }
        }

        public Task Subscribe(string stream, Action<RecordedEvent, Position> eventAppeared, Action subscriptionDropped)
        {
            var id = Guid.NewGuid();
            var source = new TaskCompletionSource<object>();

            if (_subscriptions.TryAdd(id, new Subscription(source, id, stream, eventAppeared, subscriptionDropped)))
            {
                var subscribe = new ClientMessage.SubscribeToStream(stream);
                var pkg = new TcpPackage(TcpCommand.SubscribeToStream, id, subscribe.Serialize());
                _connection.EnqueueSend(pkg.AsByteArray());
            }
            else
            {
                source.SetException(new Exception("Failed to add subscription. Concurrency failure."));
            }

            return source.Task;
        }

        public void Unsubscribe(string stream)
        {
            foreach (var id in _subscriptions.Values.Where(s => s.Stream == stream).Select(s => s.Id))
            {
                Subscription removed;
                if(_subscriptions.TryRemove(id, out removed))
                {
                    removed.Source.SetResult(null);
                    ExecuteUserCallbackAsync(removed.SubscriptionDropped);

                    var pkg = new TcpPackage(TcpCommand.UnsubscribeFromStream,
                                             id,
                                             new ClientMessage.UnsubscribeFromStream(stream).Serialize());
                    _connection.EnqueueSend(pkg.AsByteArray());
                }
            }
        }

        public Task SubscribeToAllStreams(Action<RecordedEvent, Position> eventAppeared, Action subscriptionDropped)
        {
            var id = Guid.NewGuid();
            var source = new TaskCompletionSource<object>();

            if (_subscriptions.TryAdd(id, new Subscription(source, id, eventAppeared, subscriptionDropped)))
            {
                var subscribe = new ClientMessage.SubscribeToAllStreams();
                var pkg = new TcpPackage(TcpCommand.SubscribeToAllStreams, id, subscribe.Serialize());
                _connection.EnqueueSend(pkg.AsByteArray());
            }
            else
            {
                source.SetException(new Exception("Failed to add subscription to all streams. Concurrency failure"));
            }
            
            return source.Task;
        }

        public void UnsubscribeFromAllStreams()
        {
            var all = _subscriptions.Values;
            var ids = all.Where(s => s.Stream == null).Select(s => s.Id);

            foreach (var id in ids)
            {
                Subscription removed;
                if (_subscriptions.TryRemove(id, out removed))
                {
                    removed.Source.SetResult(null);
                    ExecuteUserCallbackAsync(removed.SubscriptionDropped);

                    var pkg = new TcpPackage(TcpCommand.UnsubscribeFromAllStreams, 
                                             id, 
                                             new ClientMessage.UnsubscribeFromAllStreams().Serialize());
                    _connection.EnqueueSend(pkg.AsByteArray());
                }
            }
        }

        private void ExecuteUserCallbackAsync(Action callback)
        {
            _executionQueue.Enqueue(callback);
        }

        private void ExecuteUserCallbacks()
        {
            while (!_stopExecutionThread)
            {
                Action callback;
                if (_executionQueue.TryDequeue(out callback))
                {
                    try
                    {
                        callback();
                    }
                    catch (Exception e)
                    {
                        _log.Error(e, "User callback thrown");
                    }
                }
                else
                    Thread.Sleep(1);
            }
        }

        private void OnPackageReceived(TcpTypedConnection connection, TcpPackage package)
        {
            Subscription subscription;
            if (!_subscriptions.TryGetValue(package.CorrelationId, out subscription))
            {
                _log.Error("Unexpected package received : {0} ({1})", package.CorrelationId, package.Command);
                return;
            }

            try
            {
                switch (package.Command)
                {
                    case TcpCommand.StreamEventAppeared:
                    {
                        var dto = package.Data.Deserialize<ClientMessage.StreamEventAppeared>();
                        var recordedEvent = new RecordedEvent(dto);
                        var commitPos = dto.CommitPosition;
                        var preparePos = dto.PreparePosition;
                        ExecuteUserCallbackAsync(() => subscription.EventAppeared(recordedEvent, new Position(commitPos, preparePos)));
                        break;
                    }
                    case TcpCommand.SubscriptionDropped:
                    case TcpCommand.SubscriptionToAllDropped:
                    {
                        Subscription removed;
                        if (_subscriptions.TryRemove(subscription.Id, out removed))
                        {
                            removed.Source.SetResult(null);
                            ExecuteUserCallbackAsync(removed.SubscriptionDropped);
                        }
                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException(string.Format("Unexpected command : {0}", package.Command));
                }
            }
            catch (Exception e)
            {
                _log.Error(e, "Error on package received");
            }
        }

        private void OnConnectionEstablished(TcpTypedConnection tcpTypedConnection)
        {
            _connectedEvent.Set();
        }

        private void OnConnectionClosed(TcpTypedConnection connection, IPEndPoint endPoint, SocketError error)
        {
            _connectedEvent.Reset();

            foreach (var subscription in _subscriptions.Values)
            {
                subscription.Source.SetResult(null);
                ExecuteUserCallbackAsync(subscription.SubscriptionDropped);
            }
            _subscriptions.Clear();
        }

        public void EnsureConnected(IPEndPoint endPoint)
        {
            lock (_subscriptionChannelLock)
            {
                if (!_connectedEvent.WaitOne(0))
                {
                    Connect(endPoint);
                    if (!_connectedEvent.WaitOne(ConnectionTimeoutMs))
                    {
                        var message = string.Format("Couldn't connect to [{0}] in {1} ms.", _connection.EffectiveEndPoint, ConnectionTimeoutMs);
                        _log.Error(message);
                        throw new CannotEstablishConnectionException(message);
                    }
                }
            }
        }

        private void Connect(IPEndPoint endPoint)
        {
            _connection = _connector.CreateTcpConnection(endPoint, OnPackageReceived, OnConnectionEstablished, OnConnectionClosed);

            if (_executionThread == null)
            {
                _stopExecutionThread = false;
                _executionThread = new Thread(ExecuteUserCallbacks)
                                   {
                                           IsBackground = true,
                                           Name = "SubscriptionsChannel user callbacks thread"
                                   };
                _executionThread.Start();
            }
        }
    }
}