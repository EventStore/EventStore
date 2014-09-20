using System;
using System.Collections.Generic;
using System.Diagnostics;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Data;
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
        private readonly int _startFrom;
        private bool _ready;
        internal PersistentSubscriptionClientCollection _pushClients;
        private bool _outstandingReadRequest;
        private readonly PersistentSubscriptionStats _statistics;
        private readonly Stopwatch _totalTimeWatch;
        private readonly bool _trackLatency;
        private readonly TimeSpan _messageTimeout;
        private readonly OutstandingMessageCache _outstandingMessages;
        private StreamBuffer _streamBuffer;
        private int _readBatchSize = 100; //TODO configurable
        private int _liveBufferSize;
        private int _bufferSize;
        private int _maxRetryCount;
        private PersistentSubscriptionState _state = PersistentSubscriptionState.Idle;
        private int _lastPulledEvent;
        private readonly bool _preferRoundRobin;
        private IPersistentSubscriptionCheckpointWriter _checkpointWriter;
        private int _lastCheckPoint;
        private TimeSpan _checkPointAfter;
        private int _minCheckPointCount;
        private int _maxCheckPointCount;
        private DateTime _lastCheckPointTime = DateTime.MinValue;

        public bool HasClients
        {
            get { return _pushClients.Count > 0; }
        }

        public int ClientCount { get { return _pushClients.Count; } }

        public PersistentSubscriptionState State
        {
            get { return _state; }
        }

        public PersistentSubscription(PersistentSubscriptionParams persistentSubscriptionParams)
        {
            Ensure.NotNull(persistentSubscriptionParams.EventLoader, "eventLoader");
            Ensure.NotNull(persistentSubscriptionParams.CheckpointReader, "checkpointReader");
            Ensure.NotNull(persistentSubscriptionParams.CheckpointWriter, "checkpointWriter");
            Ensure.NotNull(persistentSubscriptionParams.SubscriptionId, "subscriptionId");
            Ensure.NotNull(persistentSubscriptionParams.EventStreamId, "eventStreamId");
            Ensure.NotNull(persistentSubscriptionParams.GroupName, "groupName");
            ResolveLinkTos = persistentSubscriptionParams.ResolveLinkTos;
            SubscriptionId = persistentSubscriptionParams.SubscriptionId;
            EventStreamId = persistentSubscriptionParams.EventStreamId;
            GroupName = persistentSubscriptionParams.GroupName;
            _eventLoader = persistentSubscriptionParams.EventLoader;
            _checkpointReader = persistentSubscriptionParams.CheckpointReader;
            _checkpointWriter = persistentSubscriptionParams.CheckpointWriter;
            _lastPulledEvent = 0;
            
            //TODO refactor to state.
            _ready = false;
            _startFrom = persistentSubscriptionParams.StartFrom;
            _trackLatency = persistentSubscriptionParams.TrackLatency;
            _messageTimeout = persistentSubscriptionParams.MessageTimeout;
            _preferRoundRobin = persistentSubscriptionParams.PreferRoundRobin;
            _maxRetryCount = persistentSubscriptionParams.MaxRetryCount;
            _liveBufferSize = persistentSubscriptionParams.LiveBufferSize;
            _bufferSize = persistentSubscriptionParams.HistoryBufferSize;
            _checkPointAfter = persistentSubscriptionParams.CheckPointAfter;
            _minCheckPointCount = persistentSubscriptionParams.MinCheckPointCount;
            _maxCheckPointCount = persistentSubscriptionParams.MaxCheckPointCount;
            _totalTimeWatch = new Stopwatch();
            _totalTimeWatch.Start();
            _statistics = new PersistentSubscriptionStats(this, _totalTimeWatch);
            _outstandingMessages = new OutstandingMessageCache();
            InitAsNew();
        }

        public void InitAsNew()
        {
            _ready = false;
            _lastCheckPoint = -1;
            _statistics.SetLastKnownEventNumber(-1);
            _outstandingReadRequest = false;
            _checkpointReader.BeginLoadState(SubscriptionId, OnCheckpointLoaded);
            _pushClients = new PersistentSubscriptionClientCollection(_preferRoundRobin);
        }

        private void OnCheckpointLoaded(int? checkpoint)
        {
            _ready = true;
            if (!checkpoint.HasValue)
            {
                if (_startFrom >= 0) _lastPulledEvent = _startFrom;
                _streamBuffer = new StreamBuffer(_bufferSize, _liveBufferSize, -1, _startFrom >= 0);
                TryReadingNewBatch();
            }
            else
            {
                _lastPulledEvent = checkpoint.Value;
                _streamBuffer = new StreamBuffer(_bufferSize, _liveBufferSize, -1, true);
                TryReadingNewBatch();
            }
        }


        public void TryReadingNewBatch()
        {
            if (_outstandingReadRequest) return;
            if (_streamBuffer.Live) return;
            if (!_streamBuffer.CanAccept(_readBatchSize)) return;
            _outstandingReadRequest = true;
            _eventLoader.BeginLoadState(this, _lastPulledEvent, _readBatchSize, HandleReadCompleted);
        }

        public void HandleReadCompleted(ResolvedEvent[] events, int newposition)
        {
            if (!_ready) return;
            _outstandingReadRequest = false; //mark not in read (even if we break the loop can be restarted then)
            if (events.Length == 0)
            {
                _streamBuffer.MoveToLive();
                return;
            }
            foreach (var ev in events)
            {
                _streamBuffer.AddReadMessage(new OutstandingMessage(ev.OriginalEvent.EventId, null, ev, 0));
            }
            _lastPulledEvent = newposition;
            TryReadingNewBatch();
            TryPushingMessagesToClients();
        }

        public void TryPushingMessagesToClients()
        {
            if (!_ready) return;
            while(true)
            {
                OutstandingMessage message;
                if (!_streamBuffer.TryPeek(out message)) return;
                if (!_pushClients.PushMessageToClient(message.ResolvedEvent)) return;
                if (!_streamBuffer.TryDequeue(out message))
                {
                    throw new WTFException("This should never happen. Something is very wrong in the threading model.");
                }
                MarkBeginProcessing(message);
            }
        }

        public void NotifyLiveSubscriptionMessage(ResolvedEvent resolvedEvent)
        {
            if (!_ready) return;
            _statistics.SetLastKnownEventNumber(resolvedEvent.OriginalEventNumber);
            _streamBuffer.AddLiveMessage(new OutstandingMessage(resolvedEvent.OriginalEvent.EventId, null, resolvedEvent, 0));
            TryPushingMessagesToClients();
        }

        public IEnumerable<ResolvedEvent> GetNextNOrLessMessages(int count)
        {
            for (int i = 0; i < count; i++)
            {
                OutstandingMessage message;
                if (_streamBuffer.TryDequeue(out message))
                {
                    MarkBeginProcessing(message);
                    yield return message.ResolvedEvent;
                }
            }
        }

        private void MarkBeginProcessing(OutstandingMessage message)
        {
            _statistics.IncrementProcessed();
            _outstandingMessages.StartMessage(message, DateTime.Now + _messageTimeout);
        }

        public void AddClient(Guid correlationId, Guid connectionId, IEnvelope envelope, int maxInFlight, string user, string @from)
        {
            var client = new PersistentSubscriptionClient(correlationId, connectionId, envelope, maxInFlight, user, @from, _totalTimeWatch, _trackLatency);
            _pushClients.AddClient(client);
            TryPushingMessagesToClients();
        }

        public void Shutdown()
        {
            _pushClients.ShutdownAll();
        }

        public void RemoveClientByConnectionId(Guid connectionId)
        {
            var lostMessages = _pushClients.RemoveClientByConnectionId(connectionId);
            //TODO this are in an arbitrary order right now it may be nicer if
            //we would sort them (or sort them all in the outstaanding message queue
            //eg use a priority queueu there
            foreach (var m in lostMessages)
            {
                RetryMessage(m, 0);
            }
        }

        public void RemoveClientByCorrelationId(Guid correlationId, bool sendDropNotification)
        {
            _pushClients.RemoveClientByCorrelationId(correlationId, sendDropNotification);
        }

        public void TryMarkCheckpoint(bool isTimeCheck)
        {
            var lowest = _outstandingMessages.GetLowestPosition();
            var difference = lowest - _lastCheckPoint;
            var now = DateTime.Now;
            var timedifference = now - _lastCheckPointTime;
            if(timedifference > _checkPointAfter)
            if ((difference >= _minCheckPointCount && isTimeCheck) || difference >= _maxCheckPointCount)
            {
                _lastCheckPointTime = now;
                _lastCheckPoint = lowest;
                _checkpointWriter.BeginWriteState(lowest);
                _statistics.SetLastCheckPoint(lowest);
            }
        }

        public void AddMessageAsProcessing(ResolvedEvent ev, PersistentSubscriptionClient client)
        {
            _outstandingMessages.StartMessage(new OutstandingMessage(ev.Event.EventId, client, ev, 0), DateTime.Now + _messageTimeout);
        }

        public void AcknowledgeMessagesProcessed(Guid correlationId, Guid[] processedEventIds)
        {
            _pushClients.AcknowledgeMessagesProcessed(correlationId, processedEventIds);
            foreach (var id in processedEventIds)
            {
                _outstandingMessages.Remove(id);
            }
            TryMarkCheckpoint(false);
            TryReadingNewBatch();
            TryPushingMessagesToClients();
        }

        private void RevertToCheckPoint()
        {
            Log.Debug("Reverting future reads to checkpoint.");
            InitAsNew();
        }

        public void NotifyClockTick()
        {
            foreach (var message in _outstandingMessages.GetMessagesExpiringBefore(DateTime.Now))
            {
                if(!ActionTakenForPoisonMessage(message))
                    RetryMessage(message.ResolvedEvent, message.RetryCount + 1);
            }
            TryMarkCheckpoint(true);
        }

        private bool ActionTakenForPoisonMessage(OutstandingMessage message)
        {
            //TODO some configurable strategy for poison messages
            return false;
        }


        //TODO retry needs to be cleaned a bit between connections and outstanding
        private void RetryMessage(ResolvedEvent @event, int count)
        {
            _streamBuffer.AddRetry(new OutstandingMessage(@event.Event.EventId, null, @event, count + 1));
        }

        public MonitoringMessage.SubscriptionInfo GetStatistics()
        {
            return _statistics.GetStatistics();
        }
    }

    public class WTFException : Exception
    {
        public WTFException(string message) : base(message)
        {
        }
    }
}