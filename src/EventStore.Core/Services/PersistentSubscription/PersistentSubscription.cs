using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        public string SubscriptionId { get { return _settings.SubscriptionId; } }
        public string EventStreamId { get { return _settings.EventStreamId; } }
        public string GroupName { get { return _settings.GroupName; } }
        public bool ResolveLinkTos { get { return _settings.ResolveLinkTos; } }
        private bool _ready;
        internal PersistentSubscriptionClientCollection _pushClients;
        private bool _outstandingReadRequest;
        private readonly PersistentSubscriptionStats _statistics;
        private readonly Stopwatch _totalTimeWatch;
        private readonly OutstandingMessageCache _outstandingMessages;
        private StreamBuffer _streamBuffer;
        private PersistentSubscriptionState _state = PersistentSubscriptionState.Idle;
        private int _lastPulledEvent;
        private int _lastCheckPoint;
        private DateTime _lastCheckPointTime = DateTime.MinValue;
        private readonly PersistentSubscriptionParams _settings;
        private int _lastKnownMessage;

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
            Ensure.NotNull(persistentSubscriptionParams.MessageParker, "messageParker");
            Ensure.NotNull(persistentSubscriptionParams.SubscriptionId, "subscriptionId");
            Ensure.NotNull(persistentSubscriptionParams.EventStreamId, "eventStreamId");
            Ensure.NotNull(persistentSubscriptionParams.GroupName, "groupName");
            _lastPulledEvent = 0;            
            //TODO refactor to state.
            _ready = false;
            _totalTimeWatch = new Stopwatch();
            _settings = persistentSubscriptionParams;
            _totalTimeWatch.Start();
            _statistics = new PersistentSubscriptionStats(this, _settings, _totalTimeWatch);
            _outstandingMessages = new OutstandingMessageCache();
            InitAsNew();
        }

        public void InitAsNew()
        {
            _ready = false;
            _lastCheckPoint = -1;
            _statistics.SetLastKnownEventNumber(-1);
            _outstandingReadRequest = false;
            _settings.CheckpointReader.BeginLoadState(SubscriptionId, OnCheckpointLoaded);
            _pushClients = new PersistentSubscriptionClientCollection(_settings.PreferRoundRobin);
        }

        private void OnCheckpointLoaded(int? checkpoint)
        {
            _ready = true;
            if (!checkpoint.HasValue)
            {
                if (_settings.StartFrom >= 0) _lastPulledEvent = _settings.StartFrom;
                _streamBuffer = new StreamBuffer(_settings.BufferSize, _settings.LiveBufferSize, -1, _settings.StartFrom >= 0);
                TryReadingNewBatch();
            }
            else
            {
                _lastPulledEvent = checkpoint.Value;
                _streamBuffer = new StreamBuffer(_settings.BufferSize, _settings.LiveBufferSize, -1, true);
                TryReadingNewBatch();
            }
        }

        public void TryReadingNewBatch()
        {
            if (_outstandingReadRequest) return;
            if (_streamBuffer.Live) return;
            if (!_streamBuffer.CanAccept(_settings.ReadBatchSize)) return;
            _outstandingReadRequest = true;
            _settings.EventLoader.BeginReadEvents(this, _lastPulledEvent, _settings.ReadBatchSize, HandleReadCompleted);
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
                if (message.ResolvedEvent.OriginalEventNumber > _lastKnownMessage) _lastKnownMessage = message.ResolvedEvent.OriginalEventNumber;
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
            _outstandingMessages.StartMessage(message, DateTime.Now + _settings.MessageTimeout);
        }

        public void AddClient(Guid correlationId, Guid connectionId, IEnvelope envelope, int maxInFlight, string user, string @from)
        {
            var client = new PersistentSubscriptionClient(correlationId, connectionId, envelope, maxInFlight, user, @from, _totalTimeWatch, _settings.ExtraStatistics);
            _pushClients.AddClient(client);
            TryPushingMessagesToClients();
        }

        public void Shutdown()
        {
            _pushClients.ShutdownAll();
        }

        public void RemoveClientByConnectionId(Guid connectionId)
        {
            var lostMessages = _pushClients.RemoveClientByConnectionId(connectionId).OrderBy(v => v.OriginalEventNumber);
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
            //TODO? COMPETING better to make -1? as of now we are inclusive of checkpoint.
            //TODO COMPETING need to check if buffered has lower due to retry into buffer!
            if (lowest == int.MinValue) lowest = _lastKnownMessage;
            //no outstanding messages. in this case we can say that the last known
            //event would be our checkpoint place (we have already completed it)
            var difference = lowest - _lastCheckPoint;
            var now = DateTime.Now;
            var timedifference = now - _lastCheckPointTime;
            if (timedifference < _settings.CheckPointAfter && difference < _settings.MaxCheckPointCount) return;
            if ((difference >= _settings.MinCheckPointCount && isTimeCheck) ||
                difference >= _settings.MaxCheckPointCount)
            {
                _lastCheckPointTime = now;
                _lastCheckPoint = lowest;
                _settings.CheckpointWriter.BeginWriteState(lowest);
                _statistics.SetLastCheckPoint(lowest);
            }
        }

        public void AddMessageAsProcessing(ResolvedEvent ev, PersistentSubscriptionClient client)
        {
            _outstandingMessages.StartMessage(new OutstandingMessage(ev.Event.EventId, client, ev, 0), DateTime.Now + _settings.MessageTimeout);
        }

        public void AcknowledgeMessagesProcessed(Guid correlationId, Guid[] processedEventIds)
        {
            RemoveProcessingMessages(correlationId, processedEventIds);
            TryMarkCheckpoint(false);
            TryReadingNewBatch();
            TryPushingMessagesToClients();
        }

        public void NotAcknowledgeMessagesProcessed(Guid correlationId, Guid[] processedEventIds, NakAction action, string reason)
        {
            foreach (var id in processedEventIds)
            {
                Log.Info("Message NAK'ed id {0} action to take {1} reason '{2}'", id, action, reason ?? "");
                HandleNakedMessage(action, id, reason);
            }
            RemoveProcessingMessages(correlationId, processedEventIds);
            TryMarkCheckpoint(false);
            TryReadingNewBatch();
            TryPushingMessagesToClients();
        }

        private void HandleNakedMessage(NakAction action, Guid id, string reason)
        {
            OutstandingMessage e;
            switch (action)
            {
                case NakAction.Retry:
                case NakAction.Unknown:
                    if (_outstandingMessages.GetMessageById(id, out e))
                    {
                        RetryMessage(e.ResolvedEvent, e.RetryCount + 1);
                    }
                    break;
                case NakAction.Park:
                    if (_outstandingMessages.GetMessageById(id, out e))
                    {
                        ParkMessage(e.ResolvedEvent, "Client explicitly NAK'ed message.\n" + reason, 0);
                    }
                    break;
                case NakAction.Stop:
                    StopSubscription();
                    break;
                case NakAction.Skip:
                    SkipMessage(id);
                    break;
                default:
                    SkipMessage(id);
                    break;
            }
        }

        private void ParkMessage(ResolvedEvent resolvedEvent, string reason, int count)
        {
            _settings.MessageParker.BeginParkMessage(resolvedEvent, reason, (e, result) =>
            {
                if (result != OperationResult.Success)
                {
                    if (count < 5)
                    {
                        Log.Info("Unable to park message {0}/{1} operation failed {2} retrying.", e.OriginalStreamId,
                        e.OriginalEventNumber, result);
                        ParkMessage(e, reason, count + 1);
                        return;
                    }
                    Log.Error("Unable to park message {0}/{1} operation failed {2} after retries. possible message loss.", e.OriginalStreamId,
                        e.OriginalEventNumber, result);
                }
                _outstandingMessages.Remove(e.Event.EventId);
            });
        }

        private void SkipMessage(Guid id)
        {
            _outstandingMessages.Remove(id);
        }

        private void StopSubscription()
        {
            //TODO CC Stop subscription?
        }

        private void RemoveProcessingMessages(Guid correlationId, Guid[] processedEventIds)
        {
            _pushClients.RemoveProcessingMessages(correlationId, processedEventIds);
            foreach (var id in processedEventIds)
            {
                _outstandingMessages.Remove(id);
            }
        }

        private void RevertToCheckPoint()
        {
            Log.Debug("Reverting future reads to checkpoint.");
            InitAsNew();
        }

        public void NotifyClockTick(DateTime time)
        {
            foreach (var message in _outstandingMessages.GetMessagesExpiringBefore(time))
            {
                if (!ActionTakenForRetriedMessage(message))
                {
                    RetryMessage(message.ResolvedEvent, message.RetryCount + 1);
                }
            }
            TryPushingMessagesToClients();
            TryMarkCheckpoint(true);
        }

        private bool ActionTakenForRetriedMessage(OutstandingMessage message)
        {
            if (message.RetryCount < _settings.MaxRetryCount) return false;
            ParkMessage(message.ResolvedEvent, string.Format("Reached retry count of {0}", _settings.MaxRetryCount), 0);
            return true;
        }

        private void RetryMessage(ResolvedEvent @event, int count)
        {
            Log.Debug("Retrying message {0} {1}/{2}", SubscriptionId, @event.OriginalStreamId, @event.OriginalPosition);
            _outstandingMessages.Remove(@event.Event.EventId);
            _pushClients.RemoveProcessingMessage(@event);
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