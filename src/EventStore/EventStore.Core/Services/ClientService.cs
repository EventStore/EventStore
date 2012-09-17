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
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Tcp;

namespace EventStore.Core.Services
{
    public class ClientService : IHandle<TcpMessage.ConnectionClosed>,
                                 IHandle<ClientMessage.SubscribeToStream>,
                                 IHandle<ClientMessage.UnsubscribeFromStream>,
                                 IHandle<ClientMessage.SubscribeToAllStreams>,
                                 IHandle<ClientMessage.UnsubscribeFromAllStreams>,
                                 IHandle<ReplicationMessage.EventCommited>
    {
        public const int ConnectionQueueSizeThreshold = 10000;

        private readonly List<Tuple<Guid, TcpConnectionManager>> _subscribedToAll = new List<Tuple<Guid, TcpConnectionManager>>();
        private readonly Dictionary<string, List<Tuple<Guid, TcpConnectionManager>>> _subscriptions = new Dictionary<string, List<Tuple<Guid, TcpConnectionManager>>>();

        private readonly List<Tuple<Guid, TcpConnectionManager>> _pendingUnsubscribe = new List<Tuple<Guid, TcpConnectionManager>>();
        private readonly List<Tuple<Guid, TcpConnectionManager>> _pendingUnsubscribeAll = new List<Tuple<Guid, TcpConnectionManager>>();

        public void Handle(TcpMessage.ConnectionClosed message)
        {
            _subscribedToAll.RemoveAll(x => x.Item2 == message.Connection);
            foreach (var subscribers in _subscriptions.Values)
            {
                subscribers.RemoveAll(x => x.Item2 == message.Connection);
            }
        }

        public void Handle(ClientMessage.SubscribeToStream message)
        {
            List<Tuple<Guid, TcpConnectionManager>> subscribers;
            if (!_subscriptions.TryGetValue(message.EventStreamId, out subscribers))
            {
                subscribers = new List<Tuple<Guid, TcpConnectionManager>>();
                _subscriptions.Add(message.EventStreamId, subscribers);
            }
            subscribers.Add(Tuple.Create(message.CorrelationId, message.Connection));
        }

        public void Handle(ClientMessage.UnsubscribeFromStream message)
        {
            UnsubscribeFromStream(message.EventStreamId, message.CorrelationId, message.Connection);
        }

        public void Handle(ClientMessage.SubscribeToAllStreams message)
        {
            _subscribedToAll.Add(Tuple.Create(message.CorrelationId, message.Connection));
        }

        public void Handle(ClientMessage.UnsubscribeFromAllStreams message)
        {
            UnsubscribeFromAllStreams(message.CorrelationId, message.Connection);
        }

        public void Handle(ReplicationMessage.EventCommited message)
        {
            foreach (var tuple in _subscribedToAll)
            {
                var manager = tuple.Item2;

                if (manager.SendQueueSize <= ConnectionQueueSizeThreshold)
                    manager.SendMessage(new ClientMessage.StreamEventAppeared(tuple.Item1, message.EventNumber, message.Prepare));
                else
                {
                    _pendingUnsubscribeAll.Add(tuple);
                    manager.SendMessage(new ClientMessage.SubscriptionToAllDropped(tuple.Item1));
                }
            }

            if (_pendingUnsubscribeAll.Count > 0)
            {
                foreach (var tuple in _pendingUnsubscribeAll)
                {
                    UnsubscribeFromAllStreams(tuple.Item1, tuple.Item2);
                }
            }

            List<Tuple<Guid, TcpConnectionManager>> subscribers;
            var eventStreamId = message.Prepare.EventStreamId;
            if (_subscriptions.TryGetValue(eventStreamId, out subscribers))
            {
                foreach (var tuple in subscribers)
                {
                    var manager = tuple.Item2;
                    if (manager.SendQueueSize <= ConnectionQueueSizeThreshold)
                        manager.SendMessage(new ClientMessage.StreamEventAppeared(tuple.Item1, message.EventNumber, message.Prepare));
                    else
                    {
                        _pendingUnsubscribe.Add(Tuple.Create(tuple.Item1, tuple.Item2));
                        manager.SendMessage(new ClientMessage.SubscriptionDropped(tuple.Item1, eventStreamId));
                    }
                }

                if (_pendingUnsubscribe.Count > 0)
                {
                    foreach (var tuple in _pendingUnsubscribe)
                    {
                        UnsubscribeFromStream(eventStreamId, tuple.Item1, tuple.Item2);
                    }
                }
            }
        }

        private void UnsubscribeFromStream(string eventStreamId, Guid correlationId, TcpConnectionManager connection)
        {
            List<Tuple<Guid, TcpConnectionManager>> subscribers;
            if (_subscriptions.TryGetValue(eventStreamId, out subscribers))
                subscribers.Remove(Tuple.Create(correlationId, connection));
        }

        private void UnsubscribeFromAllStreams(Guid correlationId, TcpConnectionManager connection)
        {
            _subscribedToAll.Remove(Tuple.Create(correlationId, connection));
        }
    }
}
