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
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class HeadingEventReader
    {
        private readonly ILogger _logger = LogManager.GetLoggerFor<HeadingEventReader>();
        private IEventReader _headEventReader;
        private EventPosition _subscribeFromPosition = new EventPosition(long.MaxValue, long.MaxValue);

        private readonly Queue<ReaderSubscriptionMessage.CommittedEventDistributed> _lastMessages =
            new Queue<ReaderSubscriptionMessage.CommittedEventDistributed>();

        private readonly int _eventCacheSize;

        private readonly Dictionary<Guid, IReaderSubscription> _headSubscribers =
            new Dictionary<Guid, IReaderSubscription>();

        private bool _headEventReaderPaused;
        private Guid _eventReaderId;
        private bool _started;
        private EventPosition _lastEventPosition = new EventPosition(0, -1);

        public HeadingEventReader(int eventCacheSize)
        {
            _eventCacheSize = eventCacheSize;
        }

        public bool Handle(ReaderSubscriptionMessage.CommittedEventDistributed message)
        {
            EnsureStarted();
            if (message.CorrelationId != _eventReaderId)
                return false;
            if (message.Data == null)
                return true;
            ValidateEventOrder(message);

            CacheRecentMessage(message);
            DistributeMessage(message);
            if (_headSubscribers.Count == 0 && !_headEventReaderPaused)
            {
//                _headEventReader.Pause();
//                _headEventReaderPaused = true;
            }
            return true;
        }

        public bool Handle(ReaderSubscriptionMessage.EventReaderIdle message)
        {
            EnsureStarted();
            if (message.CorrelationId != _eventReaderId)
                return false;
            DistributeMessage(message);
            return true;
        }

        public bool Handle(ReaderSubscriptionMessage.EventReaderEof message)
        {
            throw new NotImplementedException();
        }

        private void ValidateEventOrder(ReaderSubscriptionMessage.CommittedEventDistributed message)
        {
            if (_lastEventPosition >= message.Data.Position)
                throw new InvalidOperationException(
                    string.Format(
                        "Invalid committed event order.  Last: '{0}' Received: '{1}'", _lastEventPosition,
                        message.Data.Position));
            _lastEventPosition = message.Data.Position;
        }

        public void Start(Guid eventReaderId, IEventReader eventReader)
        {
            if (_started)
                throw new InvalidOperationException("Already started");
            _eventReaderId = eventReaderId;
            _headEventReader = eventReader;
            //Guid.Empty means head distribution point
            _headEventReader.Resume();
            _started = true;
        }

        public void Stop()
        {
            EnsureStarted();
            _headEventReader.Pause();
            _headEventReader = null;
            _started = false;
        }

        public bool TrySubscribe(
            Guid projectionId, IReaderSubscription readerSubscription, long fromTransactionFilePosition)
        {
            EnsureStarted();
            if (_headSubscribers.ContainsKey(projectionId))
                throw new InvalidOperationException(
                    string.Format("Projection '{0}' has been already subscribed", projectionId));
            // if first available event commit position is before the safe TF (prepare) position - join
            if (_subscribeFromPosition.CommitPosition <= fromTransactionFilePosition)
            {
                _logger.Trace(
                    "The '{0}' subscription has joined the heading distribution point at '{1}'", projectionId,
                    fromTransactionFilePosition);
                DispatchRecentMessagesTo(readerSubscription, fromTransactionFilePosition);
                AddSubscriber(projectionId, readerSubscription);
                return true;
            }
            return false;
        }

        public void Unsubscribe(Guid projectionId)
        {
            EnsureStarted();
            if (!_headSubscribers.ContainsKey(projectionId))
                throw new InvalidOperationException(
                    string.Format("Projection '{0}' has not been subscribed", projectionId));
            _logger.Trace(
                "The '{0}' subscription has unsubscribed from the '{1}' heading distribution point", projectionId,
                _eventReaderId);
            _headSubscribers.Remove(projectionId);
        }

        private void DispatchRecentMessagesTo(
            IHandle<ReaderSubscriptionMessage.CommittedEventDistributed> subscription,
            long fromTransactionFilePosition)
        {
            foreach (var m in _lastMessages)
                if (m.Data.Position.CommitPosition >= fromTransactionFilePosition)
                    subscription.Handle(m);
        }

        private void DistributeMessage(ReaderSubscriptionMessage.CommittedEventDistributed message)
        {
            foreach (var subscriber in _headSubscribers.Values)
                subscriber.Handle(message);
        }

        private void DistributeMessage(ReaderSubscriptionMessage.EventReaderIdle message)
        {
            foreach (var subscriber in _headSubscribers.Values)
                subscriber.Handle(message);
        }

        private void CacheRecentMessage(ReaderSubscriptionMessage.CommittedEventDistributed message)
        {
            _lastMessages.Enqueue(message);
            if (_lastMessages.Count > _eventCacheSize)
            {
                _lastMessages.Dequeue();
            }
            var lastAvailableCommittedevent = _lastMessages.Peek();
            _subscribeFromPosition = lastAvailableCommittedevent.Data.Position;
        }

        private void AddSubscriber(Guid publishWithCorrelationId, IReaderSubscription subscription)
        {
            _logger.Trace(
                "The '{0}' projection subscribed to the '{1}' heading distribution point", publishWithCorrelationId,
                _eventReaderId);
            _headSubscribers.Add(publishWithCorrelationId, subscription);
            if (_headEventReaderPaused)
            {
                _headEventReaderPaused = false;
                //_headEventReader.Resume();
            }
        }

        private void EnsureStarted()
        {
            if (!_started)
                throw new InvalidOperationException("Not started");
        }
    }
}
