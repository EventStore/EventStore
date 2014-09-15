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
        private readonly bool _startFromBeginning;
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

        private PersistentSubscriptionState _state = PersistentSubscriptionState.Idle;
        private int _lastPulledEvent;
        private bool _preferOne;
        private IPersistentSubscriptionCheckpointWriter _checkpointWriter;

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
            _startFromBeginning = persistentSubscriptionParams.StartFromBeginning;
            _trackLatency = persistentSubscriptionParams.TrackLatency;
            _messageTimeout = persistentSubscriptionParams.MessageTimeout;
            _preferOne = persistentSubscriptionParams.PreferOne;
            _totalTimeWatch = new Stopwatch();
            _totalTimeWatch.Start();
            _statistics = new PersistentSubscriptionStats(this, _totalTimeWatch);
            _outstandingMessages = new OutstandingMessageCache();
            //TODO make configurable
            InitAsNew();
        }

        public void InitAsNew()
        {
            _ready = false;
            _statistics.SetLastKnownEventNumber(-1);
            _outstandingReadRequest = false;
            //TODO make configurable buffer sizes
            //TODO allow init from position
            _checkpointReader.BeginLoadState(SubscriptionId, OnCheckpointLoaded);
            _pushClients = new PersistentSubscriptionClientCollection(_preferOne);
        }

        private void OnCheckpointLoaded(int? checkpoint)
        {
            _ready = true;
            if (!checkpoint.HasValue)
            {
                if (_startFromBeginning)
                {
                    _lastPulledEvent = 0;
                    _streamBuffer = new StreamBuffer(1000, 500, -1, true);
                }
                else
                {
                    _streamBuffer = new StreamBuffer(1000, 500, -1, false);
                }
                TryReadingNewBatch();
            }
            else
            {
                _lastPulledEvent = checkpoint.Value;
                _streamBuffer = new StreamBuffer(1000, 500, -1, true);
                TryReadingNewBatch();
            }
        }


        public void TryReadingNewBatch()
        {
            if (_outstandingReadRequest) return;
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
            TryPushingMessagesToClients();
            TryReadingNewBatch();
        }

        public void TryPushingMessagesToClients()
        {
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

        public void MarkCheckpoint()
        {
            //TODO we probably want some various checkpointing strategies not just
            //do every message or every n messages. time based/message count
            //are probably good. We may also want to try writing our checkpoints
            //during a proper shutdown to make things more friendly for clients
            //that dont want to understand the intricacies of dealing with atleast
            //once messaging (less chance of a duplicate). This is worth a conversation
            //with james (better to teach early?)
            var lowest = _outstandingMessages.GetLowestPosition();
            lowest = lowest < 0 ? 0 : lowest;
            _checkpointWriter.BeginWriteState(lowest);
            _statistics.SetLastEventNumnber(lowest);
            Log.Debug("writing checkpoint. " + lowest);
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
                if(id.GetHashCode() % 10 == 0) MarkCheckpoint(); //TODO umm yeah please dont laugh at this.
                _outstandingMessages.Remove(id);
            }
            TryReadingNewBatch();
        }

        private void RevertToCheckPoint()
        {
            Log.Debug("Reverting future reads to checkpoint.");
            InitAsNew();
        }

        public void InvalidateMessagesNeedingRetry()
        {
            foreach (var message in _outstandingMessages.GetMessagesExpiringBefore(DateTime.Now))
            {
                if(!ActionTakenForPoisonMessage(message))
                    RetryMessage(message.ResolvedEvent, message.RetryCount + 1);
            }
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