using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Core.Data;
using EventStore.Core.DataStructures;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.PersistentSubscription
{
    public class PersistentSubscription
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<PersistentSubscription>();

        public readonly string SubscriptionId;
        public readonly bool ResolveLinkTos;
        public readonly string EventStreamId;
        public readonly string GroupName;
        private readonly IPersistentSubscriptionEventLoader _eventLoader;
        private readonly IPersistentSubscriptionCheckpointReader _checkpointReader;
        private readonly IPersistentSubscriptionCheckpointWriter _checkpointWriter;
        private readonly bool _startFromBeginning;
        private Dictionary<Guid, PersistentSubscriptionClient> _clients = new Dictionary<Guid, PersistentSubscriptionClient>();
        private bool _outstandingFetchRequest;
        private int _lastEventNumber = -1;
        private PersistentSubscriptionState _state;
        private long _totalItems;
        private readonly Stopwatch _totalTimeWatch;
        private TimeSpan _lastTotalTime;
        private long _lastTotalItems;
        private readonly bool _trackLatency;
        private readonly TimeSpan _messageTimeout;
        private readonly Dictionary<Guid, ResolvedEvent> _outstandingRequests = new Dictionary<Guid, ResolvedEvent>();
        private readonly PairingHeap<MessageReceipt> _receipts = new PairingHeap<MessageReceipt>(); 
        private readonly BoundedQueue<ResolvedEvent> _liveEvents = new BoundedQueue<ResolvedEvent>(500); 

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
            bool startFromBeginning,
            bool trackLatency,
            TimeSpan messageTimeout,
            IPersistentSubscriptionEventLoader eventLoader,
            IPersistentSubscriptionCheckpointReader checkpointReader,
            IPersistentSubscriptionCheckpointWriter checkpointWriter
            )
        {
            ResolveLinkTos = resolveLinkTos;
            SubscriptionId = subscriptionId;
            EventStreamId = eventStreamId;
            GroupName = groupName;
            _eventLoader = eventLoader;
            _checkpointReader = checkpointReader;
            _checkpointWriter = checkpointWriter;
            _startFromBeginning = startFromBeginning;
            _trackLatency = trackLatency;
            _messageTimeout = messageTimeout;
            _totalTimeWatch = new Stopwatch();
            _totalTimeWatch.Start();
            InitAsNew();
        }

        public void InitAsNew()
        {
            _lastEventNumber = -1;
            _outstandingFetchRequest = false;
            _state = PersistentSubscriptionState.Pull;
            _clients = new Dictionary<Guid, PersistentSubscriptionClient>();
            _checkpointReader.BeginLoadState(SubscriptionId, OnStateLoaded);
        }

        private void OnStateLoaded(int? lastProcessedEvent)
        {
            if (lastProcessedEvent.HasValue)
            {
                _lastEventNumber = lastProcessedEvent.Value;
                _state = PersistentSubscriptionState.Pull;
                FetchNewEventsBatch();
            }
            else
            {
                if (_startFromBeginning)
                {
                    _state = PersistentSubscriptionState.Pull;
                    _lastEventNumber = -1;
                    FetchNewEventsBatch();
                }
                else
                {
                    _state = PersistentSubscriptionState.Push;
                }

            }
        }

        private bool FetchNewEventsBatch()
        {
            var inFlight = _clients.Values.Sum(x => x.InFlightMessages);
            if (inFlight > 0)
            {
                _outstandingFetchRequest = true;
                //_eventLoader.BeginLoadState(this, _nextEventNumber, inFlight, HandleReadEvents);
                return true;
            }
            return false;
        }


        public void AddClient(Guid correlationId, Guid connectionId, IEnvelope envelope, int maxInFlight, string user, string @from)
        {
            var client = new PersistentSubscriptionClient(correlationId, connectionId, envelope, maxInFlight, user, @from, _totalTimeWatch, _trackLatency);
            _clients[correlationId] = client;
        }

        public bool HasAnyClients
        {
            get { return _clients.Count > 0; }
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
            var clients = _clients.Values.Where(x => x.ConnectionId == connectionId).ToList();
            foreach (var client in clients)
            {
                _clients.Remove(client.CorrelationId);
                var unconfirmedEvents = client.GetUnconfirmedEvents();
                Log.Debug("Client {0} disconnected. Rerouting unconfirmed events.", client.ConnectionId);
                foreach (var evnt in unconfirmedEvents)
                {
                    RerouteEvent(evnt);
                }
            }
        }

        public void TimerInvalidate()
        {
            //check for timed out messages in pairing heap
        }

        private void RerouteEvent(SequencedEvent evnt)
        {
            throw new NotImplementedException();
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
                    //HandleUnhandledEvent(evnt);
                }
            }
        }

        public void Push(ResolvedEvent resolvedEvent)
        {
            //event happened
        }

        public void MarkCheckpoint()
        {
            //write checkpoint
        }

        public void AcknowledgeMessagesProcessed(Guid correlationId, Guid[] processedEventIds)
        {
            PersistentSubscriptionClient client;
            if (_clients.TryGetValue(correlationId, out client))
            {
                var processedEvents = client.ConfirmProcessing(processedEventIds);
                foreach (var processedEvent in processedEvents)
                {

                }
            }
        }

        public void HandleReadEvents(ResolvedEvent[] events)
        {
            //callback from read
        }

        private void RevertToCheckPoint()
        {
            Log.Debug("No clients, reverting future to checkpoint.");
            InitAsNew();
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
                var latencyStats = conn.GetLatencyStats();
                var stats = latencyStats == null ? null : latencyStats.Measurements;
                connections.Add(new MonitoringMessage.ConnectionInfo
                {
                    From = conn.From,
                    Username = conn.Username,
                    AverageItemsPerSecond = connAvgItemsPerSecond,
                    TotalItems = conn.TotalItems,
                    CountSinceLastMeasurement = connLastItems,
                    LatencyStats = stats
                });
            }
            return new MonitoringMessage.SubscriptionInfo()
            {
                EventStreamId = EventStreamId,
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