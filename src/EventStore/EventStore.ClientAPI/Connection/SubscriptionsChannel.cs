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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI.Common.Log;
using EventStore.ClientAPI.Common.Utils;
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

        private readonly ManualResetEventSlim _connectedEvent = new ManualResetEventSlim(false);
        private readonly object _subscriptionChannelLock = new object();
        private readonly Common.Concurrent.ConcurrentQueue<Action> _executionQueue = new Common.Concurrent.ConcurrentQueue<Action>(); 
        private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, SubscriptionTaskPair> _subscriptions = new System.Collections.Concurrent.ConcurrentDictionary<Guid, SubscriptionTaskPair>();
        private int _workerRunning;

        public SubscriptionsChannel(TcpConnector connector)
        {
            _connector = connector;
            _log = LogManager.GetLogger();
        }

        public void Close()
        {
            lock (_subscriptionChannelLock)
            {
                if (_connection != null)
                    _connection.Close();
            }
        }

        public Task<EventStoreSubscription> Subscribe(string streamId,
                                                      bool resolveLinkTos,
                                                      Action<EventStoreSubscription, ResolvedEvent> eventAppeared, 
                                                      Action<EventStoreSubscription> subscriptionDropped)
        {
            var id = Guid.NewGuid();

            var eventStoreSubscription = new EventStoreSubscription(id, streamId, this, eventAppeared, subscriptionDropped);
            var subscriptionTaskPair = new SubscriptionTaskPair(eventStoreSubscription);
            if (!_subscriptions.TryAdd(id, subscriptionTaskPair))
                throw new Exception("Failed to add subscription. Concurrency failure.");

            var subscribe = new ClientMessage.SubscribeToStream(streamId, resolveLinkTos);
            var pkg = new TcpPackage(TcpCommand.SubscribeToStream, id, subscribe.Serialize());
            _connection.EnqueueSend(pkg.AsByteArray());

            return subscriptionTaskPair.TaskCompletionSource.Task;
        }

        public void Unsubscribe(Guid correlationId)
        {
            if (DropSubscription(correlationId))
            {
                var pkg = new TcpPackage(TcpCommand.UnsubscribeFromStream,
                                         correlationId,
                                         new ClientMessage.UnsubscribeFromStream().Serialize());
                _connection.EnqueueSend(pkg.AsByteArray());
            }
        }

        private bool DropSubscription(Guid correlationId)
        {
            SubscriptionTaskPair subscription;
            if (_subscriptions.TryRemove(correlationId, out subscription))
            {
                ExecuteUserCallbackAsync(subscription.Subscription.SubscriptionDropped);
                return true;
            }
            return false;
        }

        private void ExecuteUserCallbackAsync(Action callback)
        {
            _executionQueue.Enqueue(callback);

            if (Interlocked.CompareExchange(ref _workerRunning, 1, 0) == 0)
                ThreadPool.QueueUserWorkItem(ExecuteUserCallbacks);
        }

        private void ExecuteUserCallbacks(object state)
        {
            bool proceed = true;
            while (proceed)
            {
                Action callback;
                while (_executionQueue.TryDequeue(out callback))
                {
                    try
                    {
                        callback();
                    }
                    catch (Exception e)
                    {
                        _log.Error(e, "User callback have thrown.");
                    }
                }

                Interlocked.CompareExchange(ref _workerRunning, 0, 1);

                // try to reacquire lock if needed
                proceed = _executionQueue.Count > 0 && Interlocked.CompareExchange(ref _workerRunning, 1, 0) == 0; 
            }
        }

        private void OnPackageReceived(TcpTypedConnection connection, TcpPackage package)
        {
            if (package.Command == TcpCommand.BadRequest && package.CorrelationId == Guid.Empty)
            {
                // TODO AN reports somehow to client code, that something bad happened
                var message = Encoding.UTF8.GetString(package.Data.Array, package.Data.Offset, package.Data.Count);
                throw new Exception(string.Format("BadRequest received from server. Error: {0}",
                                                  string.IsNullOrEmpty(message) ? "<no message>" : message));
            }

            SubscriptionTaskPair subscription;
            if (!_subscriptions.TryGetValue(package.CorrelationId, out subscription))
            {
                _log.Error("Unexpected package received: {0} ({1})", package.CorrelationId, package.Command);
                return;
            }

            try
            {
                switch (package.Command)
                {
                    case TcpCommand.SubscriptionConfirmation:
                    {
                        var dto = package.Data.Deserialize<ClientMessage.SubscriptionConfirmation>();
                        subscription.Subscription.ConfirmSubscription(dto.LastCommitPosition, dto.LastEventNumber);
                        subscription.TaskCompletionSource.SetResult(subscription.Subscription);
                        break;
                    }
                    case TcpCommand.StreamEventAppeared:
                    {
                        var dto = package.Data.Deserialize<ClientMessage.StreamEventAppeared>();
                        ExecuteUserCallbackAsync(() => subscription.Subscription.EventAppeared(new ResolvedEvent(dto.Event)));
                        break;
                    }
                    case TcpCommand.SubscriptionDropped:
                    {
                        DropSubscription(package.CorrelationId);
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

            foreach (var correlationId in _subscriptions.Keys)
            {
                DropSubscription(correlationId);
            }
        }

        public void EnsureConnected(IPEndPoint endPoint)
        {
            lock (_subscriptionChannelLock)
            {
                if (!_connectedEvent.Wait(0))
                {
                    Connect(endPoint);
                    if (!_connectedEvent.Wait(ConnectionTimeoutMs))
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
        }

        private class SubscriptionTaskPair
        {
            public readonly TaskCompletionSource<EventStoreSubscription> TaskCompletionSource;
            public readonly EventStoreSubscription Subscription;

            public SubscriptionTaskPair(EventStoreSubscription subscription)
            {
                Ensure.NotNull(subscription, "subscription");
                Subscription = subscription;
                TaskCompletionSource = new TaskCompletionSource<EventStoreSubscription>();
            }
        }
    }
}