using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Services.UserManagement;
using ReadStreamResult = EventStore.Core.Data.ReadStreamResult;

namespace EventStore.Core.Services.PersistentSubscription
{
    public class PersistentSubscriptionService :
                                        IHandle<SystemMessage.BecomeShuttingDown>,
                                        IHandle<TcpMessage.ConnectionClosed>,
                                        IHandle<SystemMessage.BecomeMaster>,
                                        IHandle<SystemMessage.StateChangeMessage>,
                                        IHandle<ClientMessage.ConnectToPersistentSubscription>,
                                        IHandle<StorageMessage.EventCommitted>,
                                        IHandle<ClientMessage.UnsubscribeFromStream>,
                                        IHandle<ClientMessage.PersistentSubscriptionNotifyEventsProcessed>,
                                        IHandle<ClientMessage.CreatePersistentSubscription>,
                                        IHandle<ClientMessage.DeletePersistentSubscription>,
                                        IHandle<MonitoringMessage.GetAllPersistentSubscriptionStats>,
                                        IHandle<MonitoringMessage.GetPersistentSubscriptionStats>,
                                        IHandle<MonitoringMessage.GetStreamPersistentSubscriptionStats>
    {
        public const string AllStreamsSubscriptionId = ""; // empty stream id means subscription to all streams

        private static readonly ILogger Log = LogManager.GetLoggerFor<PersistentSubscriptionService>();

        private Dictionary<string, List<PersistentSubscription>> _subscriptionTopics;
        private Dictionary<string, PersistentSubscription> _subscriptionsById;

        private readonly IQueuedHandler _queuedHandler;
        private readonly IReadIndex _readIndex;
        private readonly IODispatcher _ioDispatcher;
        private readonly IPersistentSubscriptionCheckpointReader _checkpointReader;
        private readonly IPersistentSubscriptionEventLoader _eventLoader;
        private PersistentSubscriptionConfig _config = new PersistentSubscriptionConfig();
        private bool _started = false;
        private VNodeState _state;

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

        public void InitToEmpty()
        {
            _subscriptionTopics = new Dictionary<string, List<PersistentSubscription>>();
            _subscriptionsById = new Dictionary<string, PersistentSubscription>(); 
        }

        public void Handle(SystemMessage.StateChangeMessage message)
        {
            _state = message.State;

            if (message.State != VNodeState.Master)
            {
                ShutdownSubscriptions();
                Stop();
            }
        }

        public void Handle(SystemMessage.BecomeMaster message)
        {
            InitToEmpty();
            LoadConfiguration(Start);
        }


        public void Handle(SystemMessage.BecomeShuttingDown message)
        {
            ShutdownSubscriptions();
            Stop();
            _queuedHandler.RequestStop();
        }

        private void ShutdownSubscriptions()
        {
            foreach (var subscription in _subscriptionsById.Values)
            {
                subscription.Shutdown();
            }
        }

        private void Start()
        {
            _started = true;
        }

        private void Stop()
        {
            _started = false;
        }

        public void Handle(ClientMessage.UnsubscribeFromStream message)
        {
            if (!_started) return;
            UnsubscribeFromStream(message.CorrelationId, true);
        }

        public void Handle(ClientMessage.CreatePersistentSubscription message)
        {
            if (!_started) return;
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
            var key = BuildSubscriptionGroupKey(message.EventStreamId, message.GroupName);
            if (_subscriptionsById.ContainsKey(key))
            {
                message.Envelope.ReplyWith(new ClientMessage.CreatePersistentSubscriptionCompleted(message.CorrelationId,
                    ClientMessage.CreatePersistentSubscriptionCompleted.CreatePersistentSubscriptionResult.AlreadyExists, 
                    "Group '" + message.GroupName + "' already exists."));
                return;
            }
            CreateSubscriptionGroup(message.EventStreamId, message.GroupName, message.ResolveLinkTos);
            Log.Debug("New persistent subscription {0}.", message.GroupName);
            _config.Updated = DateTime.Now;
            _config.UpdatedBy = message.User.Identity.Name;
            _config.Entries.Add(new PersistentSubscriptionEntry(){Stream=message.EventStreamId, Group = message.GroupName, ResolveLinkTos = message.ResolveLinkTos});            
            SaveConfiguration(() => message.Envelope.ReplyWith(new ClientMessage.CreatePersistentSubscriptionCompleted(message.CorrelationId,
                ClientMessage.CreatePersistentSubscriptionCompleted.CreatePersistentSubscriptionResult.Success, "")));
        }

        private void CreateSubscriptionGroup(string eventStreamId, string groupName, bool resolveLinkTos)
        {
            var key = BuildSubscriptionGroupKey(eventStreamId, groupName);
            List<PersistentSubscription> subscribers;
            if (!_subscriptionTopics.TryGetValue(eventStreamId, out subscribers))
            {
                subscribers = new List<PersistentSubscription>();
                _subscriptionTopics.Add(eventStreamId, subscribers);
            }

            var subscription = new PersistentSubscription(
                                    resolveLinkTos, 
                                    key,
                                    eventStreamId.IsEmptyString() ? AllStreamsSubscriptionId : eventStreamId,
                                    groupName,
                                    _eventLoader, 
                                    _checkpointReader, 
                                    new PersistentSubscriptionCheckpointWriter(groupName, _ioDispatcher));
            _subscriptionsById[key] = subscription;
            subscribers.Add(subscription);
        }

        public void Handle(ClientMessage.DeletePersistentSubscription message)
        {
            if (!_started) return;
            Log.Debug("delete subscription " + message.GroupName);
            var streamAccess = _readIndex.CheckStreamAccess(SystemStreams.SettingsStream, StreamAccessType.Write, message.User);

            if (!streamAccess.Granted)
            {
                message.Envelope.ReplyWith(new ClientMessage.DeletePersistentSubscriptionCompleted(message.CorrelationId,
                                    ClientMessage.DeletePersistentSubscriptionCompleted.DeletePersistentSubscriptionResult.AccessDenied,
                                    "You do not have permissions to create streams"));
                return;
            }
            var key = BuildSubscriptionGroupKey(message.EventStreamId, message.GroupName);
            if (!_subscriptionsById.ContainsKey(key))
            {
                message.Envelope.ReplyWith(new ClientMessage.DeletePersistentSubscriptionCompleted(message.CorrelationId,
                    ClientMessage.DeletePersistentSubscriptionCompleted.DeletePersistentSubscriptionResult.DoesNotExist,
                    "Group '" + message.GroupName + "' doesn't exist."));
                return;
            }
            if (!_subscriptionTopics.ContainsKey(message.EventStreamId))
            {
                message.Envelope.ReplyWith(new ClientMessage.DeletePersistentSubscriptionCompleted(message.CorrelationId,
                    ClientMessage.DeletePersistentSubscriptionCompleted.DeletePersistentSubscriptionResult.Fail,
                    "Group '" + message.GroupName + "' doesn't exist."));
                return;

            }
            List<PersistentSubscription> subscribers;
            _subscriptionsById.Remove(key);
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

            _config.Updated = DateTime.Now;
            _config.UpdatedBy = message.User.Identity.Name;
            //TODO CC better handling of config vs live data
            var index = _config.Entries.FindLastIndex(x => x.Stream == message.EventStreamId && x.Group == message.GroupName);
            _config.Entries.RemoveAt(index);
            SaveConfiguration(() => message.Envelope.ReplyWith(new ClientMessage.DeletePersistentSubscriptionCompleted(message.CorrelationId,
    ClientMessage.DeletePersistentSubscriptionCompleted.DeletePersistentSubscriptionResult.Success, "")));

        }

        private void UnsubscribeFromStream(Guid correlationId, bool sendDropNotification)
        {
            foreach (var subscription in _subscriptionsById.Values)
            {
                subscription.RemoveClientByCorrelationId(correlationId, sendDropNotification);
            }
        }

        public void Handle(TcpMessage.ConnectionClosed message)
        {
            if (!_started) return;
            foreach (var subscription in _subscriptionsById.Values)
            {
                subscription.RemoveClientByConnectionId(message.Connection.ConnectionId);
            }
        }

        public void Handle(ClientMessage.ConnectToPersistentSubscription message)
        {
            if (!_started) return;
            var streamAccess = _readIndex.CheckStreamAccess(
                message.EventStreamId, StreamAccessType.Read, message.User);

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
            var key = BuildSubscriptionGroupKey(message.EventStreamId, message.SubscriptionId);
            PersistentSubscription subscription;
            if (!_subscriptionsById.TryGetValue(key, out subscription))
            {
                message.Envelope.ReplyWith(new ClientMessage.SubscriptionDropped(message.CorrelationId, SubscriptionDropReason.NotFound));
                return;
            }
            Log.Debug("New connection to persistent subscription {0}.", message.SubscriptionId);
            var lastEventNumber = _readIndex.GetStreamLastEventNumber(message.EventStreamId);
            var lastCommitPos = _readIndex.LastCommitPosition;
            var subscribedMessage = new ClientMessage.PersistentSubscriptionConfirmation(message.CorrelationId, lastCommitPos, lastEventNumber);
            message.Envelope.ReplyWith(subscribedMessage);
            var name = message.User == null ? "anonymous" : message.User.Identity.Name;
            subscription.AddClient(message.CorrelationId, message.ConnectionId, message.Envelope, message.NumberOfFreeSlots,name,message.From);
        }

        private static string BuildSubscriptionGroupKey(string stream, string groupName)
        {
            return stream + "::" + groupName;
        }

        public void Handle(StorageMessage.EventCommitted message)
        {
            if (!_started) return;
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
            if (!_started) return;
            PersistentSubscription subscription;
            //TODO competing adjust the naming of SubscriptionId vs GroupName
            Console.WriteLine("processed " + message.ProcessedEventIds[0] + " : " + message.ProcessedEventIds.Length);
            if (_subscriptionsById.TryGetValue(message.SubscriptionId, out subscription))
            {
                subscription.NotifyFreeSlots(message.CorrelationId, message.NumberOfFreeSlots, message.ProcessedEventIds);
            }
        }

        private void LoadConfiguration(Action continueWith)
        {
            _ioDispatcher.ReadBackward(SystemStreams.PersistentSubscriptionConfig, -1, 1, false,
                SystemAccount.Principal, x => HandleLoadCompleted(continueWith, x));
        }

        private void HandleLoadCompleted(Action continueWith, ClientMessage.ReadStreamEventsBackwardCompleted readStreamEventsBackwardCompleted)
        {
            switch (readStreamEventsBackwardCompleted.Result)
            {
                case ReadStreamResult.Success:
                    try
                    {
                        _config =
                            PersistentSubscriptionConfig.FromSerializedForm(
                                readStreamEventsBackwardCompleted.Events[0].Event.Data);
                        foreach (var entry in _config.Entries)
                        {
                            CreateSubscriptionGroup(entry.Stream, entry.Group, entry.ResolveLinkTos);
                        }
                        continueWith();
                    }
                    catch (Exception ex)
                    {
                        Log.Error("There was an error loading configuration from storage something is wrong.", ex);
                    }
                    break;
                case ReadStreamResult.NoStream:
                    _config = new PersistentSubscriptionConfig {Version = "1"};
                    continueWith();
                    break;
                default:
                    throw new Exception(readStreamEventsBackwardCompleted.Result + " is an unexpected result writing subscription configuration. Something is wrong.");
            }
        }

        private void SaveConfiguration(Action continueWith)
        {
            Log.Debug("Saving Confiugration.");
            var data = _config.GetSerializedForm();
            var ev = new Event(Guid.NewGuid(), "PersistentConfig1", true, data, new byte[0]);
            _ioDispatcher.WriteEvent(SystemStreams.PersistentSubscriptionConfig, ExpectedVersion.Any, ev, SystemAccount.Principal, x => HandleSaveConfigurationCompleted(continueWith, x));
        }

        private void HandleSaveConfigurationCompleted(Action continueWith, ClientMessage.WriteEventsCompleted obj)
        {
            switch (obj.Result)
            {
                case OperationResult.Success:
                    continueWith();
                    break;
                case OperationResult.CommitTimeout:
                case OperationResult.PrepareTimeout:
                    Log.Info("Timeout while trying to save subscription configuration.");
                    SaveConfiguration(continueWith);
                    break;
                default:
                    throw new Exception(obj.Result + " is an unexpected result writing subscription configuration. Something is wrong.");
            }
        }

        public void LoadSubscriptionsFromConfig()
        {
            Log.Debug("Loading subscriptions from persisted config.");
            InitToEmpty();
            if(_config.Entries == null) throw new Exception("Subscription Entries should never be null.");
            foreach (var sub in _config.Entries)
            {
                CreateSubscriptionGroup(sub.Stream, sub.Group, sub.ResolveLinkTos);
            }
        }

        public void Handle(MonitoringMessage.GetPersistentSubscriptionStats message)
        {
            if (!_started)
            {
                message.Envelope.ReplyWith(new MonitoringMessage.GetPersistentSubscriptionStatsCompleted(
                    MonitoringMessage.GetPersistentSubscriptionStatsCompleted.OperationStatus.NotReady, null)
                );
                return;
            }
            List<PersistentSubscription> subscribers;
            if (!_subscriptionTopics.TryGetValue(message.EventStreamId, out subscribers) || subscribers == null)
            {
                message.Envelope.ReplyWith(new MonitoringMessage.GetPersistentSubscriptionStatsCompleted(
                    MonitoringMessage.GetPersistentSubscriptionStatsCompleted.OperationStatus.NotFound, null)
                );
                return;
            }
            var subscription = subscribers.FirstOrDefault(x => x.GroupName == message.GroupName);
            if (subscription == null)
            {
                message.Envelope.ReplyWith(new MonitoringMessage.GetPersistentSubscriptionStatsCompleted(
                    MonitoringMessage.GetPersistentSubscriptionStatsCompleted.OperationStatus.NotFound, null)
                );
                return;
            }
            var stats = new List<MonitoringMessage.SubscriptionInfo>()
            {
                subscription.GetStatistics()
            };
            message.Envelope.ReplyWith(new MonitoringMessage.GetPersistentSubscriptionStatsCompleted(
                MonitoringMessage.GetPersistentSubscriptionStatsCompleted.OperationStatus.Success, stats)
            );
        }

        public void Handle(MonitoringMessage.GetStreamPersistentSubscriptionStats message)
        {
            if (!_started)
            {
                message.Envelope.ReplyWith(new MonitoringMessage.GetPersistentSubscriptionStatsCompleted(
                        MonitoringMessage.GetPersistentSubscriptionStatsCompleted.OperationStatus.NotReady, null)
                ); 
                return;
            } 
            List<PersistentSubscription> subscribers;
            if (!_subscriptionTopics.TryGetValue(message.EventStreamId, out subscribers))
            {
                message.Envelope.ReplyWith(new MonitoringMessage.GetPersistentSubscriptionStatsCompleted(
                    MonitoringMessage.GetPersistentSubscriptionStatsCompleted.OperationStatus.NotFound, null)
                );
                return;
            }
            var stats = subscribers.Select(sub => sub.GetStatistics()).ToList();
            message.Envelope.ReplyWith(new MonitoringMessage.GetPersistentSubscriptionStatsCompleted(
                MonitoringMessage.GetPersistentSubscriptionStatsCompleted.OperationStatus.Success, stats)
            );
        }

        public void Handle(MonitoringMessage.GetAllPersistentSubscriptionStats message)
        {
            if (!_started)
            {
                message.Envelope.ReplyWith(new MonitoringMessage.GetPersistentSubscriptionStatsCompleted(
                        MonitoringMessage.GetPersistentSubscriptionStatsCompleted.OperationStatus.NotReady, null)
                );
                return;
            }
            var stats = (from subscription in _subscriptionTopics.Values from sub in subscription select sub.GetStatistics()).ToList();
            message.Envelope.ReplyWith(new MonitoringMessage.GetPersistentSubscriptionStatsCompleted(
                MonitoringMessage.GetPersistentSubscriptionStatsCompleted.OperationStatus.Success, stats)
            );
        }
    }
}
