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
        //private readonly IPersistentSubscriptionEventLoader _eventLoader;
        //private readonly IPersistentSubscriptionCheckpointWriter _checkpointWriter;
        private readonly bool _startFromBeginning;
        internal PersistentSubscriptionClientCollection _pushClients = new PersistentSubscriptionClientCollection();
        //private bool _outstandingFetchRequest;
        private readonly PersistentSubscriptionStats _statistics;
        private readonly Stopwatch _totalTimeWatch;
        private readonly bool _trackLatency;
        private readonly TimeSpan _messageTimeout;
        private readonly OutstandingMessageCache _outstandingMessages;
        private readonly SubscriptionBuffer _subscriptionBuffer;

        public bool HasClients
        {
            get { return _pushClients.Count > 0; }
        }

        public int ClientCount { get { return _pushClients.Count; } }

        public PersistentSubscriptionState State
        {
            get { return _subscriptionBuffer.State; }
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
            Ensure.NotNull(subscriptionId, "subscriptionId");
            Ensure.NotNull(eventStreamId, "eventStreamId");
            Ensure.NotNull(groupName, "groupName");
            ResolveLinkTos = resolveLinkTos;
            SubscriptionId = subscriptionId;
            EventStreamId = eventStreamId;
            GroupName = groupName;
            //_eventLoader = eventLoader;
            _checkpointReader = checkpointReader;
            //_checkpointWriter = checkpointWriter;
            _startFromBeginning = startFromBeginning;
            _trackLatency = trackLatency;
            _messageTimeout = messageTimeout;
            _totalTimeWatch = new Stopwatch();
            _totalTimeWatch.Start();
            _statistics = new PersistentSubscriptionStats(this, _totalTimeWatch);
            _outstandingMessages = new OutstandingMessageCache();
            //TODO make configurable
            InitAsNew();
        }

        public void InitAsNew()
        {
            _statistics.SetLastKnownEventNumber(-1);
            //_outstandingFetchRequest = false;
            _subscriptionBuffer = new SubscriptionBuffer(500, _statistics);
            _pushClients = new PersistentSubscriptionClientCollection();
        }


        public IEnumerable<ResolvedEvent> GetNextNOrLessMessages(int count)
        {
            return new ResolvedEvent[0];
        } 

        public void AddClient(Guid correlationId, Guid connectionId, IEnvelope envelope, int maxInFlight, string user, string @from)
        {
            var client = new PersistentSubscriptionClient(correlationId, connectionId, envelope, maxInFlight, user, @from, _totalTimeWatch, _trackLatency);
            _pushClients.AddClient(client);
        }

        public void Shutdown()
        {
            _subscriptionBuffer.Shutdown();
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
            _statistics.SetLastKnownEventNumber(resolvedEvent.OriginalEventNumber);
            _subscriptionBuffer.AddLiveMessage(resolvedEvent);
        }

        public void MarkCheckpoint()
        {
            //write checkpoint
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
        }

        private void RevertToCheckPoint()
        {
            Log.Debug("Reverting future reads to checkpoint.");
            InitAsNew();
        }

        public void InvalidateRetries()
        {
            foreach (var message in _outstandingMessages.GetMessagesExpiringBefore(DateTime.Now))
            {
                RetryMessage(message);
            }
        }

        private void RetryMessage(RetryableMessage message)
        {
            //Requeue
        }

        public MonitoringMessage.SubscriptionInfo GetStatistics()
        {
            return _statistics.GetStatistics();
        }
    }
}