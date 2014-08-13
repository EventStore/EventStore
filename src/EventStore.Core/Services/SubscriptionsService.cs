using System;
using System.Collections.Generic;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Services.TimerService;

namespace EventStore.Core.Services
{
    public enum SubscriptionDropReason
    {
        Unsubscribed = 0,
        AccessDenied = 1,
        DoesNotExist = 2
    }

    public class SubscriptionsService : IHandle<SystemMessage.SystemStart>, 
                                        IHandle<SystemMessage.BecomeShuttingDown>, 
                                        IHandle<TcpMessage.ConnectionClosed>,
                                        IHandle<ClientMessage.SubscribeToStream>,
                                        IHandle<ClientMessage.UnsubscribeFromStream>,
                                        IHandle<SubscriptionMessage.PollStream>,
                                        IHandle<SubscriptionMessage.CheckPollTimeout>,
                                        IHandle<StorageMessage.EventCommitted>
    {
        public const string AllStreamsSubscriptionId = ""; // empty stream id means subscription to all streams

        private static readonly ILogger Log = LogManager.GetLoggerFor<SubscriptionsService>();
        private static readonly TimeSpan TimeoutPeriod = TimeSpan.FromSeconds(1);

        private readonly Dictionary<string, List<Subscription>> _subscriptionTopics = new Dictionary<string, List<Subscription>>();
        private readonly Dictionary<Guid, Subscription> _subscriptionsById = new Dictionary<Guid, Subscription>();

        private readonly Dictionary<string, List<PollSubscription>> _pollTopics = new Dictionary<string, List<PollSubscription>>();
        private long _lastSeenCommitPosition = -1;

        private readonly IPublisher _bus;
        private readonly IEnvelope _busEnvelope;
        private readonly IQueuedHandler _queuedHandler;
        private readonly IReadIndex _readIndex;
        private static readonly char[] _linkToSeparator = new []{'@'};

        public SubscriptionsService(IPublisher bus, IQueuedHandler queuedHandler, IReadIndex readIndex)
        {
            Ensure.NotNull(bus, "bus");
            Ensure.NotNull(queuedHandler, "queudHandler");
            Ensure.NotNull(readIndex, "readIndex");

            _bus = bus;
            _busEnvelope = new PublishEnvelope(bus);
            _queuedHandler = queuedHandler;
            _readIndex = readIndex;
        }

        public void Handle(SystemMessage.SystemStart message)
        {
            _bus.Publish(TimerMessage.Schedule.Create(TimeoutPeriod, _busEnvelope, new SubscriptionMessage.CheckPollTimeout()));
        }

        /* SUBSCRIPTION SECTION */
        public void Handle(SystemMessage.BecomeShuttingDown message)
        {
            foreach (var subscription in _subscriptionsById.Values)
            {
                DropSubscription(subscription, sendDropNotification: true);
            }
            _queuedHandler.RequestStop();
        }

        public void Handle(TcpMessage.ConnectionClosed message)
        {
            List<string> subscriptionGroupsToRemove = null;
            foreach (var subscriptionGroup in _subscriptionTopics)
            {
                var subscriptions = subscriptionGroup.Value;
                for (int i = 0, n = subscriptions.Count; i < n; ++i)
                {
                    if (subscriptions[i].ConnectionId == message.Connection.ConnectionId)
                        _subscriptionsById.Remove(subscriptions[i].CorrelationId);
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
                    _subscriptionTopics.Remove(subscriptionGroupsToRemove[i]);
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
                                                : _readIndex.GetStreamLastEventNumber(msg.EventStreamId);
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
            if (!_subscriptionTopics.TryGetValue(eventStreamId, out subscribers))
            {
                subscribers = new List<Subscription>();
                _subscriptionTopics.Add(eventStreamId, subscribers);
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
            _subscriptionsById[correlationId] = subscription;
        }

        private void UnsubscribeFromStream(Guid correlationId)
        {
            Subscription subscription;
            if (_subscriptionsById.TryGetValue(correlationId, out subscription))
                DropSubscription(subscription, sendDropNotification: true);
        }

        private void DropSubscription(Subscription subscription, bool sendDropNotification)
        {
            if (sendDropNotification)
                subscription.Envelope.ReplyWith(
                    new ClientMessage.SubscriptionDropped(subscription.CorrelationId, SubscriptionDropReason.Unsubscribed));

            List<Subscription> subscriptions;
            if (_subscriptionTopics.TryGetValue(subscription.EventStreamId, out subscriptions))
            {
                subscriptions.Remove(subscription);
                if (subscriptions.Count == 0)
                    _subscriptionTopics.Remove(subscription.EventStreamId);
            }
            _subscriptionsById.Remove(subscription.CorrelationId);
        }

        /* LONG POLL SECTION */
        public void Handle(SubscriptionMessage.PollStream message)
        {
            if (MissedEvents(message.StreamId, message.LastCommitPosition, message.LastEventNumber))
            {
                _bus.Publish(CloneReadRequestWithNoPollFlag(message.OriginalRequest));
                return;
            }

            SubscribePoller(message.StreamId, message.ExpireAt, message.LastCommitPosition, message.LastEventNumber, message.OriginalRequest);
        }

        private bool MissedEvents(string streamId, long lastCommitPosition, int? lastEventNumber)
        {
            return _lastSeenCommitPosition > lastCommitPosition;
        }

        private void SubscribePoller(string streamId, DateTime expireAt, long lastCommitPosition, int? lastEventNumber, Message originalRequest)
        {
            List<PollSubscription> pollTopic;
            if (!_pollTopics.TryGetValue(streamId, out pollTopic))
            {
                pollTopic = new List<PollSubscription>();
                _pollTopics.Add(streamId, pollTopic);
            }
            pollTopic.Add(new PollSubscription(expireAt, lastCommitPosition, lastEventNumber ?? -1, originalRequest));
        }

        public void Handle(SubscriptionMessage.CheckPollTimeout message)
        {
            List<string> pollTopicsToRemove = null;
            var now = DateTime.UtcNow;
            foreach (var pollTopicKeyVal in _pollTopics)
            {
                var pollTopic = pollTopicKeyVal.Value;
                for (int i = pollTopic.Count - 1; i >= 0; --i)
                {
                    var poller = pollTopic[i];
                    if (poller.ExpireAt <= now)
                    {
                        _bus.Publish(CloneReadRequestWithNoPollFlag(poller.OriginalRequest));
                        pollTopic.RemoveAt(i);

                        if (pollTopic.Count == 0) // schedule removal of list instance
                        {
                            if (pollTopicsToRemove == null)
                                pollTopicsToRemove = new List<string>();
                            pollTopicsToRemove.Add(pollTopicKeyVal.Key);
                        }
                    }
                }
            }
            if (pollTopicsToRemove != null)
            {
                for (int i = 0, n = pollTopicsToRemove.Count; i < n; ++i)
                {
                    _pollTopics.Remove(pollTopicsToRemove[i]);
                }
            }
            _bus.Publish(TimerMessage.Schedule.Create(TimeSpan.FromSeconds(1), _busEnvelope, message));
        }

        private Message CloneReadRequestWithNoPollFlag(Message originalRequest)
        {
            var streamReq = originalRequest as ClientMessage.ReadStreamEventsForward;
            if (streamReq != null)
                return new ClientMessage.ReadStreamEventsForward(
                    streamReq.InternalCorrId, streamReq.CorrelationId, streamReq.Envelope,
                    streamReq.EventStreamId, streamReq.FromEventNumber, streamReq.MaxCount, streamReq.ResolveLinkTos,
                    streamReq.RequireMaster, streamReq.ValidationStreamVersion, streamReq.User);

            var allReq = originalRequest as ClientMessage.ReadAllEventsForward;
            if (allReq != null)
                return new ClientMessage.ReadAllEventsForward(
                    allReq.InternalCorrId, allReq.CorrelationId, allReq.Envelope, 
                    allReq.CommitPosition, allReq.PreparePosition, allReq.MaxCount, allReq.ResolveLinkTos,
                    allReq.RequireMaster, allReq.ValidationTfLastCommitPosition, allReq.User);

            throw new Exception(string.Format("Unexpected read request of type {0} for long polling: {1}.",
                                              originalRequest.GetType(), originalRequest));
        }

        public void Handle(StorageMessage.EventCommitted message)
        {
            _lastSeenCommitPosition = message.CommitPosition;
 
            var resolvedEvent = ProcessEventCommited(AllStreamsSubscriptionId, message.CommitPosition, message.Event, null);
            ProcessEventCommited(message.Event.EventStreamId, message.CommitPosition, message.Event, resolvedEvent);

            ReissueReadsFor(AllStreamsSubscriptionId, message.CommitPosition, message.Event.EventNumber);
            ReissueReadsFor(message.Event.EventStreamId, message.CommitPosition, message.Event.EventNumber);
        }

        private ResolvedEvent? ProcessEventCommited(
            string eventStreamId, long commitPosition, EventRecord evnt, ResolvedEvent? resolvedEvent)
        {
            List<Subscription> subscriptions;
            if (!_subscriptionTopics.TryGetValue(eventStreamId, out subscriptions))
                return resolvedEvent;
            for (int i = 0, n = subscriptions.Count; i < n; i++)
            {
                var subscr = subscriptions[i];
                if (commitPosition <= subscr.LastCommitPosition || evnt.EventNumber <= subscr.LastEventNumber)
                    continue;

                var pair = new ResolvedEvent(evnt, null, commitPosition, default(ReadEventResult));
                if (subscr.ResolveLinkTos)
                    // resolve event if has not been previously resolved
                    resolvedEvent = pair = resolvedEvent ?? ResolveLinkToEvent(evnt, commitPosition);

                subscr.Envelope.ReplyWith(new ClientMessage.StreamEventAppeared(subscr.CorrelationId, pair));
            }
            return resolvedEvent;
        }

        private ResolvedEvent ResolveLinkToEvent(EventRecord eventRecord, long commitPosition)
        {
            if (eventRecord.EventType == SystemEventTypes.LinkTo)
            {
                try
                {
                    string[] parts = Helper.UTF8NoBom.GetString(eventRecord.Data).Split(_linkToSeparator, 2);
                    int eventNumber = int.Parse(parts[0]);
                    string streamId = parts[1];

                    var res = _readIndex.ReadEvent(streamId, eventNumber);
                    if (res.Result == ReadEventResult.Success)
                        return new ResolvedEvent(res.Record, eventRecord, commitPosition, ReadEventResult.Success);
                    return new ResolvedEvent(null, eventRecord, res.Result);
                }
                catch (Exception exc)
                {
                    Log.ErrorException(exc, "Error while resolving link for event record: {0}", eventRecord.ToString());
                }
                // return unresolved link
                return new ResolvedEvent(null, eventRecord, commitPosition, ReadEventResult.Error);
            }
            return new ResolvedEvent(eventRecord, null, commitPosition, default(ReadEventResult));
        }

        private void ReissueReadsFor(string streamId, long commitPosition, int eventNumber)
        {
            List<PollSubscription> pollTopic;
            if (_pollTopics.TryGetValue(streamId, out pollTopic))
            {
                List<PollSubscription> survivors = null;
                foreach (var poller in pollTopic)
                {
                    if (commitPosition <= poller.LastCommitPosition || eventNumber <= poller.LastEventNumber)
                    {
                        if (survivors == null)
                            survivors = new List<PollSubscription>();
                        survivors.Add(poller);
                    }
                    else
                    {
                        _bus.Publish(CloneReadRequestWithNoPollFlag(poller.OriginalRequest));    
                    }
                }
                _pollTopics.Remove(streamId);
                if (survivors != null)
                    _pollTopics.Add(streamId, survivors);
            }
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

        private class PollSubscription
        {
            public readonly DateTime ExpireAt;
            public readonly long LastCommitPosition;
            public readonly int LastEventNumber;

            public readonly Message OriginalRequest;

            public PollSubscription(DateTime expireAt, long lastCommitPosition, int lastEventNumber, Message originalRequest)
            {
                ExpireAt = expireAt;
                LastCommitPosition = lastCommitPosition;
                LastEventNumber = lastEventNumber;
                OriginalRequest = originalRequest;
            }
        }
    }
}
