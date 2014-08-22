using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Common.Utils;

namespace EventStore.Core.Services.PersistentSubscription
{
    public class PersistentSubscription
    {
        private const double _minimumPercentageOfFreeSlotsForSwitchingToPushMode = 0.5;

        private static readonly ILogger Log = LogManager.GetLoggerFor<PersistentSubscription>();

        public readonly string SubscriptionId;
        public readonly bool ResolveLinkTos;
        public readonly string EventStreamId;
        public readonly string GroupName;
        private readonly IPersistentSubscriptionEventLoader _eventLoader;
        private readonly Dictionary<Guid, PersistentSubscriptionClient> _clients = new Dictionary<Guid, PersistentSubscriptionClient>();
        private ResolvedEvent[] _eventBuffer = new ResolvedEvent[0];
        private List<ResolvedEvent> _transitionBuffer;
        private readonly CheckpointingQueue _checkpointingQueue;
        private bool _outstandingFetchRequest;
        private int _nextEventNumber;
        private int _eventBufferIndex;
        private long _eventSequence;        
        private int _lastEventNumber = -1;
        private PersistentSubscriptionState _state;
        private long _totalItems;
        private Stopwatch _totalTimeWatch;
        private TimeSpan _lastTotalTime;
        private long _lastTotalItems;

        public int LastEventNumber
        {
            get { return _lastEventNumber; }
        }

        public PersistentSubscriptionState State
        {
            get { return _state; }
        }

        public PersistentSubscription(bool resolveLinkTos, 
            string subscriptionId, 
            string eventStreamId, 
            string groupName,
            IPersistentSubscriptionEventLoader eventLoader,
            IPersistentSubscriptionCheckpointReader checkpointReader,
            IPersistentSubscriptionCheckpointWriter checkpointWriter)
        {
            ResolveLinkTos = resolveLinkTos;
            SubscriptionId = subscriptionId;
            EventStreamId = eventStreamId;
            GroupName = groupName;
            _eventLoader = eventLoader;
            _checkpointingQueue = new CheckpointingQueue(checkpointWriter.BeginWriteState);
            _totalTimeWatch = new Stopwatch();
            _totalTimeWatch.Start();
            checkpointReader.BeginLoadState(subscriptionId, OnStateLoaded);
        }

        private void OnStateLoaded(int? lastProcessedEvent)
        {
            if (lastProcessedEvent.HasValue)
            {
                _lastEventNumber = lastProcessedEvent.Value;
                _nextEventNumber = _lastEventNumber + 1;
                _state = PersistentSubscriptionState.Pull;
                FetchNewEventsBatch();
            }
            else
            {
                _state = PersistentSubscriptionState.Push;
            }
        }

        public void AddClient(Guid correlationId, Guid connectionId, IEnvelope envelope, int numberOfFreeSlots, string user, string @from)
        {
            var client = new PersistentSubscriptionClient(correlationId, connectionId, envelope, numberOfFreeSlots, user, @from);
            _clients[correlationId] = client;
            if (_state != PersistentSubscriptionState.Idle)
            {
                TryPushSomeEvents();
            }
        }

        public bool HasAnyClients
        {
            get { return _clients.Any(); }
        }

        public void Shutdown()
        {
            _state = PersistentSubscriptionState.ShuttingDown;
            foreach (var client in _clients.Values)
            {
                client.SendDropNotification();
            }
        }

        public void RemoveClientByConnectionId(Guid connectionId)
        {
            var client = _clients.Values.FirstOrDefault(x => x.ConnectionId == connectionId);
            if (client != null)
            {
                _clients.Remove(client.CorrelationId);
                var unconfirmedEvents = client.GetUnconfirmedEvents();
                Log.Debug("Client {0} disconnected. Rerouting unconfirmed events.", client.ConnectionId);
                foreach (var evnt in unconfirmedEvents)
                {
                    ForcePushToAny(evnt);
                }
            }
        }
        
        public void RemoveClientByCorrelationId(Guid correlationId, bool sendDropNotification)
        {
            PersistentSubscriptionClient client;
            if (_clients.TryGetValue(correlationId, out client))
            {
                _clients.Remove(client.CorrelationId);
                if (sendDropNotification)
                {
                    client.SendDropNotification();
                }
                var unconfirmedEvents = client.GetUnconfirmedEvents();
                Log.Debug("Client {0} disconnected. Rerouting unconfirmed events.", client.ConnectionId);
                foreach (var evnt in unconfirmedEvents)
                {
                    ForcePushToAny(evnt);
                }
            }
        }

        public void Push(ResolvedEvent resolvedEvent)
        {
            if (resolvedEvent.Event.EventNumber <= LastEventNumber)
            {
                return;
            }
            if (_state == PersistentSubscriptionState.TransitioningFromPullToPush)
            {
                _transitionBuffer.Add(resolvedEvent);
            }
            else if (_state == PersistentSubscriptionState.Push)
            {
                if (!TryPushToAny(resolvedEvent))
                {
                    SwitchToPullMode(resolvedEvent);
                }    
            }
        }

        public void MarkCheckpoint()
        {
            _checkpointingQueue.MarkCheckpoint();
        }

        public void NotifyFreeSlots(Guid correlationId, int numberOfFreeSlots, Guid[] processedEventIds)
        {
            PersistentSubscriptionClient client;
            if (_clients.TryGetValue(correlationId, out client))
            {
                var processedEvents = client.ConfirmProcessing(numberOfFreeSlots, processedEventIds);
                foreach (var processedEvent in processedEvents)
                {
                    //TODO CC limit size of checkpointing queue
                    _checkpointingQueue.Enqueue(processedEvent);
                }
            }
            MarkCheckpoint();
            if (_state == PersistentSubscriptionState.Pull)
            {
                TryPushSomeEvents();
            }
        }

        public void NotifyReadCompleted(ResolvedEvent[] events, int nextEventNumber)
        {
            if (nextEventNumber != -1)
            {
                _nextEventNumber = nextEventNumber;
            }
            _outstandingFetchRequest = false;            
            if (_state == PersistentSubscriptionState.TransitioningFromPullToPush)
            {
                _eventBuffer = MergeBufferWithPushedEvents(events);
                TryPushSomeEvents();
                if (_eventBufferIndex == _eventBuffer.Length)
                {
                    _state = PersistentSubscriptionState.Push;
                    Log.Debug("Completed transitioning to push mode.");
                }
                else
                {
                    _state = PersistentSubscriptionState.Pull;
                    Log.Debug("Failed transitioning to push mode. Reverting back to pull mode.");
                }
            }
            else if (nextEventNumber == -1)
            {
                _eventBuffer = events;
                if (FetchNewEventsBatch())
                {
                    StartTransitioningToPushMode();
                }
            }
            else
            {
                _eventBuffer = events;
                TryPushSomeEvents();                
            }
        }

        private ResolvedEvent[] MergeBufferWithPushedEvents(IEnumerable<ResolvedEvent> events)
        {
            return
                events.MergeOrdered(_transitionBuffer, x => x.Event.EventNumber)
                      .Distinct(x => x.Event.EventNumber)
                      .ToArray();
        }

        private void SwitchToPullMode(ResolvedEvent resolvedEvent)
        {
            _nextEventNumber = resolvedEvent.Event.EventNumber;
            _state = PersistentSubscriptionState.Pull;
            Log.Debug("Switched to pull mode starting at {0}.",_nextEventNumber);
        }

        private bool TryPushFromBuffer()
        {
            if (TryPushToAny(_eventBuffer[_eventBufferIndex]))
            {
                _eventBufferIndex++;
                return true;
            }
            return false;
        }

        private bool TryPushToAny(ResolvedEvent resolvedEvent)
        {
            PersistentSubscriptionClient leastBusy = null;
            foreach ( var client in _clients.Values)
            {
                if (leastBusy == null || client.FreeSlots > leastBusy.FreeSlots)
                {
                    leastBusy = client;
                }
            }
            if (leastBusy == null || leastBusy.FreeSlots == 0)
            {
                return false;
            }
            PushEvent(resolvedEvent, leastBusy);
            _lastEventNumber = resolvedEvent.Event.EventNumber;
            return true;
        }

        private void PushEvent(ResolvedEvent resolvedEvent, PersistentSubscriptionClient leastBusy)
        {
            Interlocked.Increment(ref _totalItems);
            leastBusy.Push(new SequencedEvent(_eventSequence, resolvedEvent));
            _eventSequence++;
        }

        private void ForcePushToAny(SequencedEvent sequencedEvent)
        {
            PersistentSubscriptionClient leastBusy = null;
            foreach ( var client in _clients.Values)
            {
                if (leastBusy == null || client.FreeSlots > leastBusy.FreeSlots)
                {
                    leastBusy = client;
                }
            }
            //TODO CC this needs to merge back!
            if (leastBusy != null)
            {
                Interlocked.Increment(ref _totalItems);
                leastBusy.Push(sequencedEvent);
            }
        }

        private void TryPushSomeEvents()
        {
            while (_eventBufferIndex < _eventBuffer.Length && TryPushFromBuffer())
            {
                //NOOP
            }
            if (_eventBufferIndex == _eventBuffer.Length && _state == PersistentSubscriptionState.Pull && !_outstandingFetchRequest)
            {
                if (FetchNewEventsBatch() && CanSwitchToPushMode())
                {
                    StartTransitioningToPushMode();
                }
            }
        }
        
        private bool FetchNewEventsBatch()
        {
            var freeSlots = _clients.Values.Sum(x => x.FreeSlots);
            if (freeSlots > 0)
            {
                _outstandingFetchRequest = true;
                _eventBufferIndex = 0;
                _eventBuffer = new ResolvedEvent[0];
                _eventLoader.BeginLoadState(this, _nextEventNumber, freeSlots, NotifyReadCompleted);
                return true;
            }
            return false;
        }

        private void StartTransitioningToPushMode()
        {
            _state = PersistentSubscriptionState.TransitioningFromPullToPush;
            _transitionBuffer = new List<ResolvedEvent>();
            Log.Debug("Started transitioning to push mode.");
        }

        private bool CanSwitchToPushMode()
        {
            return _clients.Values.Sum(x => x.FreeSlots) > _minimumPercentageOfFreeSlotsForSwitchingToPushMode*_clients.Values.Sum(x => x.MaximumFreeSlots);
        }

        public MonitoringMessage.SubscriptionInfo GetStatistics()
        {
            var totalTime = _totalTimeWatch.Elapsed;
            var totalItems = Interlocked.Read(ref _totalItems);

            var lastRunMs = totalTime - _lastTotalTime;
            var lastItems = totalItems - _lastTotalItems;

            var avgItemsPerSecond = lastRunMs.Ticks != 0 ? (int)(TimeSpan.TicksPerSecond * lastItems / lastRunMs.Ticks) : 0;
            _lastTotalTime = totalTime;
            _lastTotalItems = totalItems;
            var connections = new List<MonitoringMessage.ConnectionInfo>();
            foreach (var conn in _clients.Values)
            {
                var connItems = conn.TotalItems;
                var connLastItems = connItems - conn.LastTotalItems;
                conn.LastTotalItems = connItems;
                var connAvgItemsPerSecond = lastRunMs.Ticks != 0 ? (int)(TimeSpan.TicksPerSecond * connLastItems / lastRunMs.Ticks) : 0;
                connections.Add(new MonitoringMessage.ConnectionInfo
                {
                    From=conn.From, 
                    Username = conn.Username,
                    AverageItemsPerSecond = connAvgItemsPerSecond,
                    TotalItems = conn.TotalItems,
                    CountSinceLastMeasurement = connLastItems
                });
            }
            return new MonitoringMessage.SubscriptionInfo()
            {
                EventStreamId=EventStreamId,
                GroupName = GroupName,
                Status = _state.ToString(),
                Connections = connections,
                AveragePerSecond = avgItemsPerSecond,
                TotalItems = totalItems,
                CountSinceLastMeasurement = lastItems
            };
        }
    }
}
