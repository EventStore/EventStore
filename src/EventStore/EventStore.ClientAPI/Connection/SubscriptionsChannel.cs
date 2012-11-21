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
        private const int degreeOfParaleism = 1;
        private readonly ILogger _log;

        private readonly TcpConnector _connector;
        private TcpTypedConnection _connection;

        private readonly ManualResetEvent _connectedEvent = new ManualResetEvent(false);
        private readonly object _subscriptionChannelLock = new object();

        private readonly BlockingCollection<Action> _executionQueue = new BlockingCollection<Action>(); 

        private readonly ConcurrentDictionary<Guid, Subscription> _subscriptions = new ConcurrentDictionary<Guid, Subscription>();
        private readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();

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

                _tokenSource.Cancel();
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
            var all = _subscriptions.Values;
            var ids = all.Where(s => s.Stream == stream).Select(s => s.Id);

            foreach (var id in ids)
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
            _executionQueue.Add(callback);
        }

        private void StartExecutingUserCallbacks()
        {
            Enumerable.Range(0, degreeOfParaleism)
                .Select(i => Task.Factory.StartNew(
                    () =>
                        {
                            foreach (var callback in _executionQueue.GetConsumingEnumerable(_tokenSource.Token))
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
                        })).ToList();
        }

        private void OnPackageReceived(TcpTypedConnection connection, TcpPackage package)
        {
            Subscription subscription;
            if(!_subscriptions.TryGetValue(package.CorrelationId, out subscription))
            {
                _log.Error("Unexpected package received : {0} ({1})", package.CorrelationId, package.Command);
                return;
            }

            try
            {
                switch (package.Command)
                {
                    case TcpCommand.StreamEventAppeared:
                        var dto = package.Data.Deserialize<ClientMessage.StreamEventAppeared>();
                        var recordedEvent = new RecordedEvent(dto);
                        var commitPos = dto.CommitPosition;
                        var preparePos = dto.PreparePosition;
                        ExecuteUserCallbackAsync(() => subscription.EventAppeared(recordedEvent, new Position(commitPos, preparePos)));
                        break;
                    case TcpCommand.SubscriptionDropped:
                    case TcpCommand.SubscriptionToAllDropped:
                        Subscription removed;
                        if(_subscriptions.TryRemove(subscription.Id, out removed))
                        {
                            removed.Source.SetResult(null);
                            ExecuteUserCallbackAsync(removed.SubscriptionDropped);
                        }
                        break;
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

            var subscriptions = _subscriptions.Values;
            _subscriptions.Clear();

            foreach (var subscription in subscriptions)
            {
                subscription.Source.SetResult(null);
                ExecuteUserCallbackAsync(subscription.SubscriptionDropped);
            }
        }

        private void Connect(IPEndPoint endPoint)
        {
            _connection = _connector.CreateTcpConnection(endPoint, OnPackageReceived, OnConnectionEstablished, OnConnectionClosed);

            StartExecutingUserCallbacks();
        }

        public void EnsureConnected(IPEndPoint endPoint)
        {
            if (!_connectedEvent.WaitOne(0))
            {
                lock (_subscriptionChannelLock)
                {
                    if (!_connectedEvent.WaitOne(0))
                    {
                        Connect(endPoint);
                        if (!_connectedEvent.WaitOne(500))
                        {
                            _log.Error("Cannot connect to {0}", _connection.EffectiveEndPoint);
                            throw new CannotEstablishConnectionException(string.Format("Cannot connect to {0}.",
                                                                                       _connection.EffectiveEndPoint));
                        }
                    }
                }
            }
        }
    }
}