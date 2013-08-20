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
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.Services
{
    public enum SubscriptionDropReason
    {
        Unsubscribed = 0,
        AccessDenied = 1
    }

    public class SubscriptionsService : IHandle<SystemMessage.BecomeShuttingDown>,
                                        IHandle<TcpMessage.ConnectionClosed>,
                                        IHandle<ClientMessage.SubscribeToStream>,
                                        IHandle<ClientMessage.UnsubscribeFromStream>,
                                        IHandle<StorageMessage.EventCommited>
    {
        public const string AllStreamsSubscriptionId = ""; // empty stream id means subscription to all streams

        private static readonly ILogger Log = LogManager.GetLoggerFor<SubscriptionsService>();

        private readonly Dictionary<string, List<Subscription>> _subscriptionGroupsByStream = new Dictionary<string, List<Subscription>>();
        private readonly Dictionary<Guid, Subscription> _subscriptionsByCorrelationId = new Dictionary<Guid, Subscription>();
        private readonly IQueuedHandler _queuedHandler;
        private readonly IReadIndex _readIndex;

        private ResolvedEvent? _lastResolvedPair;

        public SubscriptionsService(IQueuedHandler queuedHandler, IReadIndex readIndex)
        {
            Ensure.NotNull(queuedHandler, "queudHandler");
            Ensure.NotNull(readIndex, "readIndex");

            _queuedHandler = queuedHandler;
            _readIndex = readIndex;
        }

        public void Handle(SystemMessage.BecomeShuttingDown message)
        {
            foreach (var subscription in _subscriptionsByCorrelationId.Values)
            {
                DropSubscription(subscription, sendDropNotification: true);
            }
            _queuedHandler.RequestStop();
        }

        public void Handle(TcpMessage.ConnectionClosed message)
        {
            List<string> subscriptionGroupsToRemove = null;
            foreach (var subscriptionGroup in _subscriptionGroupsByStream)
            {
                var subscriptions = subscriptionGroup.Value;
                for (int i = 0, n = subscriptions.Count; i < n; ++i)
                {
                    if (subscriptions[i].ConnectionId == message.Connection.ConnectionId)
                        _subscriptionsByCorrelationId.Remove(subscriptions[i].CorrelationId);
                }
                subscriptions.RemoveAll(x => x.ConnectionId == message.Connection.ConnectionId);
                if (subscriptions.Count == 0) // schedule removal of list instance
                {
                    if (subscriptionGroupsToRemove == null)
                        subscriptionGroupsToRemove = new List<string>();
                    subscriptionGroupsToRemove.Add(subscriptionGroup.Key);
                }
            }

            if (subscriptionGroupsToRemove != null)
            {
                for (int i = 0, n = subscriptionGroupsToRemove.Count; i < n; ++i)
                {
                    _subscriptionGroupsByStream.Remove(subscriptionGroupsToRemove[i]);
                }
            }
        }

        public void Handle(ClientMessage.SubscribeToStream msg)
        {
            var streamAccess = _readIndex.CheckStreamAccess(
                msg.EventStreamId.IsEmptyString() ? SystemStreams.AllStream : msg.EventStreamId, StreamAccessType.Read, msg.User);

            if (streamAccess.Granted)
            {
                var lastEventNumber = msg.EventStreamId.IsEmptyString()
                                                ? (int?) null
                                                : _readIndex.GetLastStreamEventNumber(msg.EventStreamId);
                var lastCommitPos = _readIndex.LastCommitPosition;
                SubscribeToStream(msg.CorrelationId, msg.Envelope, msg.ConnectionId, msg.EventStreamId, 
                                    msg.ResolveLinkTos, lastCommitPos, lastEventNumber);
                var subscribedMessage = new ClientMessage.SubscriptionConfirmation(msg.CorrelationId, lastCommitPos, lastEventNumber);
                msg.Envelope.ReplyWith(subscribedMessage);
            }
            else
            {
                msg.Envelope.ReplyWith(new ClientMessage.SubscriptionDropped(msg.CorrelationId, SubscriptionDropReason.AccessDenied));
            }
        }

        public void Handle(ClientMessage.UnsubscribeFromStream message)
        {
            UnsubscribeFromStream(message.CorrelationId);
        }

        private void SubscribeToStream(Guid correlationId, IEnvelope envelope, Guid connectionId,
                                       string eventStreamId, bool resolveLinkTos, long lastCommitPosition, int? lastEventNumber)
        {
            List<Subscription> subscribers;
            if (!_subscriptionGroupsByStream.TryGetValue(eventStreamId, out subscribers))
            {
                subscribers = new List<Subscription>();
                _subscriptionGroupsByStream.Add(eventStreamId, subscribers);
            }

            // if eventStreamId is null or empty -- subscription is to all streams
            var subscription = new Subscription(correlationId,
                                                envelope,
                                                connectionId,
                                                eventStreamId.IsEmptyString() ? AllStreamsSubscriptionId : eventStreamId,
                                                resolveLinkTos,
                                                lastCommitPosition,
                                                lastEventNumber ?? -1);
            subscribers.Add(subscription);
            _subscriptionsByCorrelationId[correlationId] = subscription;
        }

        private void UnsubscribeFromStream(Guid correlationId)
        {
            Subscription subscription;
            if (_subscriptionsByCorrelationId.TryGetValue(correlationId, out subscription))
                DropSubscription(subscription, sendDropNotification: true);
        }

        private void DropSubscription(Subscription subscription, bool sendDropNotification)
        {
            if (sendDropNotification)
                subscription.Envelope.ReplyWith(
                    new ClientMessage.SubscriptionDropped(subscription.CorrelationId, SubscriptionDropReason.Unsubscribed));

            List<Subscription> subscriptions;
            if (_subscriptionGroupsByStream.TryGetValue(subscription.EventStreamId, out subscriptions))
            {
                subscriptions.Remove(subscription);
                if (subscriptions.Count == 0)
                    _subscriptionGroupsByStream.Remove(subscription.EventStreamId);
            }
            _subscriptionsByCorrelationId.Remove(subscription.CorrelationId);
        }

        public void Handle(StorageMessage.EventCommited message)
        {
            ProcessEventCommited(AllStreamsSubscriptionId, message);
            ProcessEventCommited(message.Event.EventStreamId, message);
            _lastResolvedPair = null;
        }

        private void ProcessEventCommited(string eventStreamId, StorageMessage.EventCommited msg)
        {
            List<Subscription> subscriptions;
            if (!_subscriptionGroupsByStream.TryGetValue(eventStreamId, out subscriptions)) 
                return;
            for (int i = 0, n = subscriptions.Count; i < n; i++)
            {
                var subscr = subscriptions[i];
                if (msg.CommitPosition <= subscr.LastCommitPosition || msg.Event.EventNumber <= subscr.LastEventNumber)
                    continue;

                var pair = new ResolvedEvent(msg.Event, null, msg.CommitPosition);
                if (subscr.ResolveLinkTos)
                    _lastResolvedPair = pair = _lastResolvedPair ?? ResolveLinkToEvent(msg.Event, msg.CommitPosition);

                subscr.Envelope.ReplyWith(new ClientMessage.StreamEventAppeared(subscr.CorrelationId, pair));
            }
        }

        private ResolvedEvent ResolveLinkToEvent(EventRecord eventRecord, long commitPosition)
        {
            if (eventRecord.EventType == SystemEventTypes.LinkTo)
            {
                try
                {
                    string[] parts = Helper.UTF8NoBom.GetString(eventRecord.Data).Split('@');
                    int eventNumber = int.Parse(parts[0]);
                    string streamId = parts[1];

                    var res = _readIndex.ReadEvent(streamId, eventNumber);
                    if (res.Result == ReadEventResult.Success)
                        return new ResolvedEvent(res.Record, eventRecord, commitPosition);
                }
                catch (Exception exc)
                {
                    Log.ErrorException(exc, "Error while resolving link for event record: {0}", eventRecord.ToString());
                }
            }
            return new ResolvedEvent(eventRecord, null, commitPosition);
        }

        private class Subscription
        {
            public readonly Guid CorrelationId;
            public readonly IEnvelope Envelope;
            public readonly Guid ConnectionId;

            public readonly string EventStreamId;
            public readonly bool ResolveLinkTos;
            public readonly long LastCommitPosition;
            public readonly int LastEventNumber;

            public Subscription(Guid correlationId, 
                                IEnvelope envelope,
                                Guid connectionId,
                                string eventStreamId, 
                                bool resolveLinkTos, 
                                long lastCommitPosition, 
                                int lastEventNumber)
            {
                CorrelationId = correlationId;
                Envelope = envelope;
                ConnectionId = connectionId;

                EventStreamId = eventStreamId;
                ResolveLinkTos = resolveLinkTos;
                LastCommitPosition = lastCommitPosition;
                LastEventNumber = lastEventNumber;
            }
        }
    }
}
