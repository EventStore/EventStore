using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
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
        //private readonly IPersistentSubscriptionEventLoader _eventLoader;
        private readonly IPersistentSubscriptionCheckpointReader _checkpointReader;
        //private readonly IPersistentSubscriptionCheckpointWriter _checkpointWriter;
        private readonly bool _startFromBeginning;
        private PersistentSubscriptionClientCollection _pushClients = new PersistentSubscriptionClientCollection();
        //private bool _outstandingFetchRequest;
        private int _lastEventNumber = -1;
        private PersistentSubscriptionState _state;
        private long _totalItems;
        private readonly Stopwatch _totalTimeWatch;
        private TimeSpan _lastTotalTime;
        private long _lastTotalItems;
        private readonly bool _trackLatency;
        //private readonly TimeSpan _messageTimeout;
        private readonly Dictionary<Guid, OutstandingMessage> _outstandingRequests;
        private readonly PairingHeap<MessagePromise> promises; 
        private readonly BoundedQueue<ResolvedEvent> _liveEvents;

        public bool HasClients
        {
            get { return _pushClients.Count > 0; }
        }

        public int ClientCount { get { return _pushClients.Count; } }


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
            Ensure.NotNull(eventLoader, "eventLoader");
            Ensure.NotNull(checkpointReader, "checkpointReader");
            Ensure.NotNull(checkpointWriter, "checkpointWriter");
            ResolveLinkTos = resolveLinkTos;
            SubscriptionId = subscriptionId;
            EventStreamId = eventStreamId;
            GroupName = groupName;
            //_eventLoader = eventLoader;
            _checkpointReader = checkpointReader;
            //_checkpointWriter = checkpointWriter;
            _startFromBeginning = startFromBeginning;
            _trackLatency = trackLatency;
            //_messageTimeout = messageTimeout;
            _totalTimeWatch = new Stopwatch();
            _totalTimeWatch.Start();
            _outstandingRequests = new Dictionary<Guid, OutstandingMessage>();
            promises = new PairingHeap<MessagePromise>();
            //TODO Add configuration for queue size
            _liveEvents = new BoundedQueue<ResolvedEvent>(500);
            InitAsNew();
        }

        public void InitAsNew()
        {
            _lastEventNumber = -1;
            //_outstandingFetchRequest = false;
            _state = PersistentSubscriptionState.Pull;
            _pushClients = new PersistentSubscriptionClientCollection();
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

        private void FetchNewEventsBatch()
        {
            //_outstandingFetchRequest = true;
            //_eventLoader.BeginLoadState(this, _nextEventNumber, inFlight, HandleReadEvents);
        }


        public void AddClient(Guid correlationId, Guid connectionId, IEnvelope envelope, int maxInFlight, string user, string @from)
        {
            var client = new PersistentSubscriptionClient(correlationId, connectionId, envelope, maxInFlight, user, @from, _totalTimeWatch, _trackLatency);
            _pushClients.AddClient(client);
        }

        public void Shutdown()
        {
            _state = PersistentSubscriptionState.ShuttingDown;
            _pushClients.ShutdownAll();
        }

        public void RemoveClientByConnectionId(Guid connectionId)
        {
            _pushClients.RemoveClientByConnectionId(connectionId);
        }

        public void RemoveClientByCorrelationId(Guid correlationId, bool sendDropNotification)
        {
            _pushClients.RemoveClientByCorrelationId(correlationId, sendDropNotification);
        }

        public void NotifyLiveSubscriptionMessage(ResolvedEvent resolvedEvent)
        {
            _liveEvents.Enqueue(resolvedEvent);
        }

        public void MarkCheckpoint()
        {
            //write checkpoint
        }

        public void AcknowledgeMessagesProcessed(Guid correlationId, Guid[] processedEventIds)
        {
            _pushClients.AcknowledgeMessagesProcessed(correlationId, processedEventIds);
            foreach (var id in processedEventIds)
            {
                _outstandingRequests.Remove(id);
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


        public void TimerInvalidate()
        {
            //check for timed out messages in pairing heap
            while (promises.Count > 0 && promises.FindMin().DueTime <= DateTime.Now)
            {
            }
        }

        private void RerouteEvent(ResolvedEvent evnt)
        {
            throw new NotImplementedException();
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
            foreach (var conn in _pushClients.GetAll())
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

    struct OutstandingMessage
    {
        public readonly ResolvedEvent ResolvedEvent;
        public readonly PersistentSubscriptionClient HandlingClient;
        public readonly int RetryCount;

        public OutstandingMessage(PersistentSubscriptionClient handlingClient, ResolvedEvent resolvedEvent, int retryCount) : this()
        {
            HandlingClient = handlingClient;
            ResolvedEvent = resolvedEvent;
            RetryCount = retryCount;
        }
    }
}