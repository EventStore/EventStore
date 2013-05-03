// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

using System;
using System.Collections.Generic;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Services.TimerService;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class EventReaderCoreService : 
        IHandle<ReaderCoreServiceMessage.StartReader>, 
        IHandle<ReaderCoreServiceMessage.StopReader>, 
        IHandle<ReaderSubscriptionManagement.Subscribe>, 
        IHandle<ReaderSubscriptionManagement.Unsubscribe>, 
        IHandle<ReaderSubscriptionManagement.Pause>, 
        IHandle<ReaderSubscriptionManagement.Resume>, 
        IHandle<ReaderSubscriptionMessage.CommittedEventDistributed>, 
        IHandle<ReaderSubscriptionMessage.EventReaderIdle>, 
        IHandle<ReaderSubscriptionMessage.EventReaderEof>, 
        IHandle<ReaderCoreServiceMessage.ReaderTick>
    {
        private readonly IPublisher _publisher;
        private readonly ILogger _logger = LogManager.GetLoggerFor<ProjectionCoreService>();
        private bool _stopped = true;

        private readonly Dictionary<Guid, IReaderSubscription> _subscriptions =
            new Dictionary<Guid, IReaderSubscription>();

        private readonly Dictionary<Guid, IEventReader> _eventReaders = new Dictionary<Guid, IEventReader>();

        private readonly Dictionary<Guid, Guid> _subscriptionEventReaders = new Dictionary<Guid, Guid>();
        private readonly Dictionary<Guid, Guid> _eventReaderSubscriptions = new Dictionary<Guid, Guid>();
        private readonly HashSet<Guid> _pausedSubscriptions = new HashSet<Guid>();
        private readonly HeadingEventReader _headingEventReader;
        private readonly ICheckpoint _writerCheckpoint;


        public EventReaderCoreService(
            IPublisher publisher, int eventCacheSize,
            ICheckpoint writerCheckpoint)
        {
            _publisher = publisher;
            _headingEventReader = new HeadingEventReader(eventCacheSize);
            _writerCheckpoint = writerCheckpoint;
        }

        public void Handle(ReaderSubscriptionManagement.Pause message)
        {
            if (!_pausedSubscriptions.Add(message.SubscriptionId))
                throw new InvalidOperationException("Already paused projection");
            var projectionSubscription = _subscriptions[message.SubscriptionId];
            var eventReaderId = _subscriptionEventReaders[message.SubscriptionId];
            if (eventReaderId == Guid.Empty) // head
            {
                _subscriptionEventReaders.Remove(message.SubscriptionId);
                _headingEventReader.Unsubscribe(message.SubscriptionId);
                var forkedEventReaderId = Guid.NewGuid();
                var forkedEventReader = projectionSubscription.CreatePausedEventReader(_publisher, forkedEventReaderId);
                _subscriptionEventReaders.Add(message.SubscriptionId, forkedEventReaderId);
                _eventReaderSubscriptions.Add(forkedEventReaderId, message.SubscriptionId);
                _eventReaders.Add(forkedEventReaderId, forkedEventReader);
                _publisher.Publish(
                    new ReaderSubscriptionManagement.ReaderAssignedReader(message.SubscriptionId, forkedEventReaderId));
            }
            else
            {
                _eventReaders[eventReaderId].Pause();
            }
        }

        public void Handle(ReaderSubscriptionManagement.Resume message)
        {
            if (!_pausedSubscriptions.Remove(message.SubscriptionId))
                throw new InvalidOperationException("Not a paused projection");
            var eventReader = _subscriptionEventReaders[message.SubscriptionId];
            _eventReaders[eventReader].Resume();
        }

        public void Handle(ReaderSubscriptionManagement.Subscribe message)
        {
            if (_stopped)
                return;

            var fromCheckpointTag = message.FromPosition;
            var subscriptionId = message.SubscriptionId;
            var projectionSubscription = message.ReaderStrategy.CreateReaderSubscription(
                _publisher, fromCheckpointTag, message.SubscriptionId, message.Options);
            _subscriptions.Add(subscriptionId, projectionSubscription);

            var distibutionPointCorrelationId = Guid.NewGuid();
            var eventReader = projectionSubscription.CreatePausedEventReader(_publisher, distibutionPointCorrelationId);
            _logger.Trace(
                "The '{0}' projection subscribed to the '{1}' distribution point", subscriptionId,
                distibutionPointCorrelationId);
            _eventReaders.Add(distibutionPointCorrelationId, eventReader);
            _subscriptionEventReaders.Add(subscriptionId, distibutionPointCorrelationId);
            _eventReaderSubscriptions.Add(distibutionPointCorrelationId, subscriptionId);
            _publisher.Publish(
                new ReaderSubscriptionManagement.ReaderAssignedReader(
                    subscriptionId, distibutionPointCorrelationId));
            eventReader.Resume();
        }

        public void Handle(ReaderSubscriptionManagement.Unsubscribe message)
        {
            if (!_pausedSubscriptions.Contains(message.SubscriptionId))
                Handle(new ReaderSubscriptionManagement.Pause(message.SubscriptionId));
            var eventReaderId = _subscriptionEventReaders[message.SubscriptionId];
            if (eventReaderId != Guid.Empty)
            {
                //TODO: test it
                _eventReaders.Remove(eventReaderId);
                _eventReaderSubscriptions.Remove(eventReaderId);
                _publisher.Publish(
                    new ReaderSubscriptionManagement.ReaderAssignedReader(message.SubscriptionId, Guid.Empty));
                _logger.Trace(
                    "The '{0}' subscription has unsubscribed (reader: {1})", message.SubscriptionId,
                    eventReaderId);
            }

            _pausedSubscriptions.Remove(message.SubscriptionId);
            _subscriptionEventReaders.Remove(message.SubscriptionId);
            _subscriptions.Remove(message.SubscriptionId);
        }

        public void Handle(ReaderSubscriptionMessage.CommittedEventDistributed message)
        {
            Guid projectionId;
            if (_stopped)
                return;
            if (_headingEventReader.Handle(message))
                return;
            if (!_eventReaderSubscriptions.TryGetValue(message.CorrelationId, out projectionId))
                return; // unsubscribed
            if (TrySubscribeHeadingEventReader(message, projectionId))
                return;
            if (message.Data != null) // means notification about the end of the stream/source
                _subscriptions[projectionId].Handle(message);
        }

        public void Handle(ReaderSubscriptionMessage.EventReaderIdle message)
        {
            Guid projectionId;
            if (_stopped)
                return;
            if (_headingEventReader.Handle(message))
                return;
            if (!_eventReaderSubscriptions.TryGetValue(message.CorrelationId, out projectionId))
                return; // unsubscribed
            _subscriptions[projectionId].Handle(message);
        }

        public void Handle(ReaderSubscriptionMessage.EventReaderEof message)
        {
            Guid projectionId;
            if (_stopped)
                return;
            if (!_eventReaderSubscriptions.TryGetValue(message.CorrelationId, out projectionId))
                return; // unsubscribed
            _subscriptions[projectionId].Handle(message);

            _pausedSubscriptions.Add(projectionId); // it is actually disposed -- workaround
            Handle(new ReaderSubscriptionManagement.Unsubscribe(projectionId));
        }

        public void StartReaders()
        {
//TODO: do we need to clear subscribed projections here?
            //TODO: do we need to clear subscribed distribution points here?
            _stopped = false;
            var distributionPointCorrelationId = Guid.NewGuid();
            var transactionFileReader = new TransactionFileEventReader(
                _publisher, distributionPointCorrelationId,
                new EventPosition(_writerCheckpoint.Read(), -1), new RealTimeProvider(),
                deliverEndOfTFPosition: false);
            _eventReaders.Add(distributionPointCorrelationId, transactionFileReader);
            _headingEventReader.Start(distributionPointCorrelationId, transactionFileReader);
        }

        public void StopReaders()
        {

            if (_subscriptions.Count > 0)
            {
                _logger.Info("_subscriptions is not empty after all the projections have been killed");
                _subscriptions.Clear();
            }

            if (_eventReaders.Count > 0)
            {
                _logger.Info("_eventReaders is not empty after all the projections have been killed");
                _eventReaders.Clear();
            }

            if (_subscriptionEventReaders.Count > 0)
            {
                _logger.Info("_subscriptionEventReaders is not empty after all the projections have been killed");
                _subscriptionEventReaders.Clear();
            }

            if (_eventReaderSubscriptions.Count > 0)
            {
                _logger.Info("_eventReaderSubscriptions is not empty after all the projections have been killed");
                _eventReaderSubscriptions.Clear();
            }

            _headingEventReader.Stop();
            _stopped = true;
        }

        private bool TrySubscribeHeadingEventReader(
            ReaderSubscriptionMessage.CommittedEventDistributed message, Guid projectionId)
        {
            if (_pausedSubscriptions.Contains(projectionId))
                return false;

            var projectionSubscription = _subscriptions[projectionId];

            if (message.SafeTransactionFileReaderJoinPosition == null
                || !_headingEventReader.TrySubscribe(
                    projectionId, projectionSubscription, message.SafeTransactionFileReaderJoinPosition.Value))
                return false;

            if (message.Data == null)
            {
                _logger.Trace(
                    "The '{0}' is subscribing to the heading distribution point with TF-EOF marker event at '{1}'",
                    projectionId, message.SafeTransactionFileReaderJoinPosition);
            }

            Guid eventReaderId = message.CorrelationId;
            _eventReaders[eventReaderId].Dispose();
            _eventReaders.Remove(eventReaderId);
            _eventReaderSubscriptions.Remove(eventReaderId);
            _subscriptionEventReaders[projectionId] = Guid.Empty;
            _publisher.Publish(new ReaderSubscriptionManagement.ReaderAssignedReader(message.CorrelationId, Guid.Empty));
            return true;
        }

        public void Handle(ReaderCoreServiceMessage.StartReader message)
        {
            StartReaders();
        }

        public void Handle(ReaderCoreServiceMessage.StopReader message)
        {
            StopReaders();
        }


        public void Handle(ReaderCoreServiceMessage.ReaderTick message)
        {
            message.Action();
        }
    }
}
