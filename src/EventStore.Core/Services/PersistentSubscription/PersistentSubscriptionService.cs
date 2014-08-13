using System;
using System.Collections.Generic;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.Services.PersistentSubscription
{
    public class PersistentSubscriptionService :
                                        IHandle<SystemMessage.BecomeShuttingDown>,
                                        IHandle<TcpMessage.ConnectionClosed>,
                                        IHandle<ClientMessage.ConnectToPersistentSubscription>,
                                        IHandle<StorageMessage.EventCommitted>,
                                        IHandle<ClientMessage.UnsubscribeFromStream>,
                                        IHandle<ClientMessage.PersistentSubscriptionNotifyEventsProcessed>,
                                        IHandle<ClientMessage.CreatePersistentSubscription>,
                                        IHandle<ClientMessage.DeletePersistentSubscription>,
                                        IHandle<MonitoringMessage.GetPersistentSubscriptionStats>
    {
        public const string AllStreamsSubscriptionId = ""; // empty stream id means subscription to all streams

        private static readonly ILogger Log = LogManager.GetLoggerFor<PersistentSubscriptionService>();

        private readonly Dictionary<string, List<PersistentSubscription>> _subscriptionTopics = new Dictionary<string, List<PersistentSubscription>>();
        private readonly Dictionary<string, PersistentSubscription> _subscriptionsById = new Dictionary<string, PersistentSubscription>(); 

        private readonly IQueuedHandler _queuedHandler;
        private readonly IReadIndex _readIndex;
        private readonly IODispatcher _ioDispatcher;
        private readonly IPersistentSubscriptionCheckpointReader _checkpointReader;
        private readonly IPersistentSubscriptionEventLoader _eventLoader;

        public PersistentSubscriptionService(IQueuedHandler queuedHandler, IReadIndex readIndex, IODispatcher ioDispatcher)
        {
            Ensure.NotNull(queuedHandler, "queudHandler");
            Ensure.NotNull(readIndex, "readIndex");
            Ensure.NotNull(ioDispatcher, "ioDispatcher");

            _queuedHandler = queuedHandler;
            _readIndex = readIndex;
            _ioDispatcher = ioDispatcher;
            _checkpointReader = new PersistentSubscriptionCheckpointReader(_ioDispatcher);
            _eventLoader = new PersistentSubscriptionEventLoader(_ioDispatcher);
        }

        public void Handle(SystemMessage.BecomeShuttingDown message)
        {
            foreach (var subscription in _subscriptionsById.Values)
            {
                subscription.Shutdown();
            }
            _queuedHandler.RequestStop();
        }

        public void Handle(ClientMessage.UnsubscribeFromStream message)
        {
            UnsubscribeFromStream(message.CorrelationId, true);
        }

        public void Handle(ClientMessage.CreatePersistentSubscription message)
        {
            Log.Debug("create subscription " + message.GroupName);
            //TODO revisit for permissions. maybe make admin only?
            var streamAccess = _readIndex.CheckStreamAccess(SystemStreams.SettingsStream, StreamAccessType.Write, message.User);

            if (!streamAccess.Granted)
            {
                message.Envelope.ReplyWith(new ClientMessage.CreatePersistentSubscriptionCompleted(message.CorrelationId,
                                    ClientMessage.CreatePersistentSubscriptionCompleted.CreatePersistentSubscriptionResult.AccessDenied,
                                    "You do not have permissions to create streams"));
                return;
            }

            if (_subscriptionsById.ContainsKey(message.GroupName))
            {
                message.Envelope.ReplyWith(new ClientMessage.CreatePersistentSubscriptionCompleted(message.CorrelationId,
                    ClientMessage.CreatePersistentSubscriptionCompleted.CreatePersistentSubscriptionResult.AlreadyExists, 
                    "Group " + message.GroupName + " already exists."));
                return;
            }
            List<PersistentSubscription> subscribers;
            if (!_subscriptionTopics.TryGetValue(message.EventStreamId, out subscribers))
            {
                subscribers = new List<PersistentSubscription>();
                _subscriptionTopics.Add(message.EventStreamId, subscribers);
            }

            var subscription = new PersistentSubscription(
                message.ResolveLinkTos, message.GroupName,
                message.EventStreamId.IsEmptyString() ? AllStreamsSubscriptionId : message.EventStreamId,
                _eventLoader, _checkpointReader, new PersistentSubscriptionCheckpointWriter(message.GroupName, _ioDispatcher));
            _subscriptionsById[message.GroupName] = subscription;
            subscribers.Add(subscription);
            Log.Debug("New persistent subscription {0}.", message.GroupName);

            message.Envelope.ReplyWith(new ClientMessage.CreatePersistentSubscriptionCompleted(message.CorrelationId,
                ClientMessage.CreatePersistentSubscriptionCompleted.CreatePersistentSubscriptionResult.Success, ""));
        }

        public void Handle(ClientMessage.DeletePersistentSubscription message)
        {
            Log.Debug("delete subscription " + message.GroupName);
            var streamAccess = _readIndex.CheckStreamAccess(SystemStreams.AllStream, StreamAccessType.Write, message.User);

            if (!streamAccess.Granted)
            {
                message.Envelope.ReplyWith(new ClientMessage.DeletePersistentSubscriptionCompleted(message.CorrelationId,
                                    ClientMessage.DeletePersistentSubscriptionCompleted.DeletePersistentSubscriptionResult.AccessDenied,
                                    "You do not have permissions to create streams"));
                return;
            }
            List<PersistentSubscription> subscribers;
            _subscriptionsById.Remove(message.GroupName);
            if (_subscriptionTopics.TryGetValue(message.EventStreamId, out subscribers))
            {
                for (int i = 0; i < subscribers.Count; i++)
                {
                    var sub = subscribers[i];
                    if (sub.SubscriptionId == message.GroupName)
                    {
                        sub.Shutdown();
                        subscribers.RemoveAt(i);
                        break;
                    }
                }
            }

            message.Envelope.ReplyWith(new ClientMessage.DeletePersistentSubscriptionCompleted(message.CorrelationId,
    ClientMessage.DeletePersistentSubscriptionCompleted.DeletePersistentSubscriptionResult.Success, ""));

        }

        //should we also call statistics from the stastics subsystem to write into stream?
        public void Handle(MonitoringMessage.GetPersistentSubscriptionStats message)
        {
            Log.Debug("get statistics");
        }

        private void UnsubscribeFromStream(Guid correlationId, bool sendDropNotification)
        {
            foreach (var subscription in _subscriptionsById.Values)
            {
                subscription.RemoveClientByCorrelationId(correlationId, sendDropNotification);
            }
            CleanUpDeadSubscriptions();
        }

        public void Handle(TcpMessage.ConnectionClosed message)
        {
            foreach (var subscription in _subscriptionsById.Values)
            {
                subscription.RemoveClientByConnectionId(message.Connection.ConnectionId);
            }
            CleanUpDeadSubscriptions();
        }

        private void CleanUpDeadSubscriptions()
        {/*
            var deadSubscriptions = _subscriptionsById.Values.Where(x => !x.HasAnyClients).ToList();
            foreach (var deadSubscription in deadSubscriptions)
            {
                _subscriptionsById.Remove(deadSubscription.SubscriptionId);
                Log.Debug("Subscription {0} has no more connected clients. Removing. ", deadSubscription.SubscriptionId);
            }

            List<string> subscriptionGroupsToRemove = null;
            foreach (var subscriptionGroup in _subscriptionTopics)
            {
                var subscriptions = subscriptionGroup.Value;
                foreach (var deadSubscription in deadSubscriptions)
                {
                    subscriptions.Remove(deadSubscription);
                }
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
          */
        }

        public void Handle(ClientMessage.ConnectToPersistentSubscription message)
        {
            var streamAccess = _readIndex.CheckStreamAccess(
                message.EventStreamId.IsEmptyString() ? SystemStreams.AllStream : message.EventStreamId, StreamAccessType.Read, message.User);

            if (!streamAccess.Granted)
            {
                message.Envelope.ReplyWith(new ClientMessage.SubscriptionDropped(message.CorrelationId, SubscriptionDropReason.AccessDenied));
                return;
            }

            List<PersistentSubscription> subscribers;
            if (!_subscriptionTopics.TryGetValue(message.EventStreamId, out subscribers))
            {
                //TODO this is subscription doesnt exist.
                message.Envelope.ReplyWith(new ClientMessage.SubscriptionDropped(message.CorrelationId, SubscriptionDropReason.NotFound));
                return;
            }

            PersistentSubscription subscription;
            if (!_subscriptionsById.TryGetValue(message.SubscriptionId, out subscription))
            {
                //TODO this is subscription doesnt exist
                message.Envelope.ReplyWith(new ClientMessage.SubscriptionDropped(message.CorrelationId, SubscriptionDropReason.NotFound));
                return;
            }
            Log.Debug("New connection to persistent subscription {0}.", message.SubscriptionId);
            var lastEventNumber = _readIndex.GetStreamLastEventNumber(message.EventStreamId);
            var lastCommitPos = _readIndex.LastCommitPosition;
            var subscribedMessage = new ClientMessage.PersistentSubscriptionConfirmation(message.CorrelationId, lastCommitPos, lastEventNumber);
            message.Envelope.ReplyWith(subscribedMessage);
            subscription.AddClient(message.CorrelationId, message.ConnectionId, message.Envelope, message.NumberOfFreeSlots);
        }

        public void Handle(StorageMessage.EventCommitted message)
        {
            var resolvedEvent = ProcessEventCommited(AllStreamsSubscriptionId, message.CommitPosition, message.Event, null);
            ProcessEventCommited(message.Event.EventStreamId, message.CommitPosition, message.Event, resolvedEvent);
        }

        private ResolvedEvent? ProcessEventCommited(string eventStreamId, long commitPosition, EventRecord evnt, ResolvedEvent? resolvedEvent)
        {
            List<PersistentSubscription> subscriptions;
            if (!_subscriptionTopics.TryGetValue(eventStreamId, out subscriptions)) 
                return resolvedEvent;
            for (int i = 0, n = subscriptions.Count; i < n; i++)
            {
                var subscr = subscriptions[i];
                if (subscr.State == PersistentSubscriptionState.Pull || evnt.EventNumber <= subscr.LastEventNumber)
                    continue;

                var pair = new ResolvedEvent(evnt, null, commitPosition);
                if (subscr.ResolveLinkTos)
                    resolvedEvent = pair = resolvedEvent ?? ResolveLinkToEvent(evnt, commitPosition);

                subscr.Push(pair);
            }
            return resolvedEvent;
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

        public void Handle(ClientMessage.PersistentSubscriptionNotifyEventsProcessed message)
        {
            PersistentSubscription subscription;
            if (_subscriptionsById.TryGetValue(message.SubscriptionId, out subscription))
            {
                subscription.NotifyFreeSlots(message.CorrelationId, message.NumberOfFreeSlots, message.ProcessedEventIds);
            }
        }
    }
}
