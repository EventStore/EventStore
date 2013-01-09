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
using System.Text;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Services.Transport.Tcp;
using System.Linq;

namespace EventStore.Core.Services
{
    public class SubscriptionsService : IHandle<TcpMessage.ConnectionClosed>,
                                        IHandle<ClientMessage.SubscribeToStream>,
                                        IHandle<ClientMessage.UnsubscribeFromStream>,
                                        IHandle<ClientMessage.SubscribeToAllStreams>,
                                        IHandle<ClientMessage.UnsubscribeFromAllStreams>,
                                        IHandle<StorageMessage.EventCommited>
    {
        public const int ConnectionQueueSizeThreshold = 10000;
        public const string AllStreamsSubscriptionId = ""; // empty stream id means subscription to all streams

        private static readonly ILogger Log = LogManager.GetLoggerFor<SubscriptionsService>();

        private readonly Dictionary<string, List<Subscription>> _subscriptionGroups = new Dictionary<string, List<Subscription>>();
        private readonly List<Subscription> _pendingUnsubscribe = new List<Subscription>();
        private readonly IReadIndex _readIndex;

        private EventLinkPair? _lastResolvedPair;

        private readonly QueuedHandlerThreadPool _queuedHandler;
        private readonly InMemoryBus _internalBus = new InMemoryBus("SubscriptionsBus", true, TimeSpan.FromMilliseconds(50));

        public SubscriptionsService(ISubscriber bus, IReadIndex readIndex)
        {
            Ensure.NotNull(bus, "bus");
            Ensure.NotNull(readIndex, "readIndex");

            _readIndex = readIndex;

            _queuedHandler = new QueuedHandlerThreadPool(_internalBus, "Subscriptions", false);
            _queuedHandler.Start();

            SubscribeToMessage<TcpMessage.ConnectionClosed>(bus);
            SubscribeToMessage<ClientMessage.SubscribeToStream>(bus);
            SubscribeToMessage<ClientMessage.UnsubscribeFromStream>(bus);
            SubscribeToMessage<ClientMessage.SubscribeToAllStreams>(bus);
            SubscribeToMessage<ClientMessage.UnsubscribeFromAllStreams>(bus);
            SubscribeToMessage<StorageMessage.EventCommited>(bus);
        }

        private void SubscribeToMessage<T>(ISubscriber subscriber) where T : Message
        {
            _internalBus.Subscribe((IHandle<T>)this);
            subscriber.Subscribe(_queuedHandler.WidenFrom<T, Message>());
        }

        public void Handle(TcpMessage.ConnectionClosed message)
        {
            List<string> subscriptionGroupsToRemove = null;
            foreach (var streamSubscription in _subscriptionGroups)
            {
                var subscribers = streamSubscription.Value;
                subscribers.RemoveAll(x => x.Connection == message.Connection);
                if (subscribers.Count == 0) // schedule removal of list instance
                {
                    if (subscriptionGroupsToRemove == null)
                        subscriptionGroupsToRemove = new List<string>();
                    subscriptionGroupsToRemove.Add(streamSubscription.Key);
                }
            }

            if (subscriptionGroupsToRemove != null)
            {
                for (int i = 0, n = subscriptionGroupsToRemove.Count; i < n; ++i)
                {
                    _subscriptionGroups.Remove(subscriptionGroupsToRemove[i]);
                }
            }
        }

        public void Handle(ClientMessage.SubscribeToStream message)
        {
            SubscribeToStream(message.EventStreamId, message.CorrelationId, message.Connection, message.ResolveLinkTos);
        }

        public void Handle(ClientMessage.UnsubscribeFromStream message)
        {
            UnsubscribeFromStream(message.EventStreamId, message.CorrelationId, message.Connection);
        }

        public void Handle(ClientMessage.SubscribeToAllStreams message)
        {
            SubscribeToStream(AllStreamsSubscriptionId, message.CorrelationId, message.Connection, message.ResolveLinkTos);
        }

        public void Handle(ClientMessage.UnsubscribeFromAllStreams message)
        {
            UnsubscribeFromStream(AllStreamsSubscriptionId, message.CorrelationId, message.Connection);
        }

        private void SubscribeToStream(string eventStreamId, Guid correlationId, TcpConnectionManager connection, bool resolveLinkTos)
        {
            List<Subscription> subscribers;
            if (!_subscriptionGroups.TryGetValue(eventStreamId, out subscribers))
            {
                subscribers = new List<Subscription>();
                _subscriptionGroups.Add(eventStreamId, subscribers);
            }
            subscribers.Add(new Subscription(eventStreamId, correlationId, connection, resolveLinkTos));
        }

        private void UnsubscribeFromStream(string streamId, Guid correlationId, TcpConnectionManager connection)
        {
            List<Subscription> subscriptions;
            if (!_subscriptionGroups.TryGetValue(streamId, out subscriptions))
                return;

            var subscription = subscriptions.FirstOrDefault(x => x.Connection == connection && x.CorrelationId == correlationId);
            if (subscription != null)
                DropSubscription(subscription);
        }

        public void Handle(StorageMessage.EventCommited message)
        {
            ProcessEventCommited(AllStreamsSubscriptionId, message);
            ProcessEventCommited(message.Event.EventStreamId, message);
            _lastResolvedPair = null;
        }

        private void ProcessEventCommited(string eventStreamId, StorageMessage.EventCommited message)
        {
            List<Subscription> subscriptions;
            if (!_subscriptionGroups.TryGetValue(eventStreamId, out subscriptions)) 
                return;

            for (int i = 0, n = subscriptions.Count; i < n; i++)
            {
                var subscription = subscriptions[i];
                if (subscription.Connection.SendQueueSize <= ConnectionQueueSizeThreshold)
                {
                    var pair = new EventLinkPair(message.Event);
                    if (subscription.ResolveLinkTos)
                        _lastResolvedPair = pair = _lastResolvedPair ?? ResolveLinkToEvent(message.Event);

                    subscription.Connection.SendMessage(
                        new ClientMessage.StreamEventAppeared(subscription.CorrelationId,
                                                              message.Event.EventStreamId,
                                                              message.CommitPosition,
                                                              message.Event.LogPosition,
                                                              pair));
                }
                else
                {
                    _pendingUnsubscribe.Add(subscription);
                }
            }

            for (int i = 0, n = _pendingUnsubscribe.Count; i < n; i++)
            {
                DropSubscription(_pendingUnsubscribe[i]);
            }
            _pendingUnsubscribe.Clear();
        }

        private EventLinkPair ResolveLinkToEvent(EventRecord eventRecord)
        {
            if (eventRecord.EventType == SystemEventTypes.LinkTo)
            {
                try
                {
                    string[] parts = Encoding.UTF8.GetString(eventRecord.Data).Split('@');
                    int eventNumber = int.Parse(parts[0]);
                    string streamId = parts[1];

                    var res = _readIndex.ReadEvent(streamId, eventNumber);
                    if (res.Result == SingleReadResult.Success)
                        return new EventLinkPair(res.Record, eventRecord);
                }
                catch (Exception exc)
                {
                    Log.ErrorException(exc, "Error while resolving link for event record: {0}", eventRecord.ToString());
                }
            }
            return new EventLinkPair(eventRecord);
        }

        private void DropSubscription(Subscription subscription)
        {
            if (subscription.EventStreamId == AllStreamsSubscriptionId)
                subscription.Connection.SendMessage(new ClientMessage.SubscriptionToAllDropped(subscription.CorrelationId));
            else
                subscription.Connection.SendMessage(new ClientMessage.SubscriptionDropped(subscription.CorrelationId, subscription.EventStreamId));

            List<Subscription> subscriptions;
            if (_subscriptionGroups.TryGetValue(subscription.EventStreamId, out subscriptions))
            {
                subscriptions.Remove(subscription);
                if (subscriptions.Count == 0)
                    _subscriptionGroups.Remove(subscription.EventStreamId);
            }
        }

        private class Subscription
        {
            public readonly string EventStreamId;
            public readonly Guid CorrelationId;
            public readonly TcpConnectionManager Connection;
            public readonly bool ResolveLinkTos;

            public Subscription(string eventStreamId, Guid correlationId, TcpConnectionManager connection, bool resolveLinkTos)
            {
                EventStreamId = eventStreamId;
                CorrelationId = correlationId;
                Connection = connection;
                ResolveLinkTos = resolveLinkTos;
            }
        }
    }
}
