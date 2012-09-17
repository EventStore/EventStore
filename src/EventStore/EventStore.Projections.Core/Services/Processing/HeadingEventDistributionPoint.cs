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
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class HeadingEventDistributionPoint
    {
        private TransactionFileReaderEventDistributionPoint _headDistributionPoint;
        private EventPosition _subscribeFromPosition = new EventPosition(long.MaxValue, long.MaxValue);

        private readonly Queue<ProjectionMessage.Projections.CommittedEventReceived> _lastMessages =
            new Queue<ProjectionMessage.Projections.CommittedEventReceived>();

        private readonly int _eventCacheSize;
        private readonly Dictionary<Guid, ProjectionSubscription> _headSubscribers = new Dictionary<Guid, ProjectionSubscription>();
        private bool _headDistributionPointPaused;
        private Guid _distributionPointId;

        public HeadingEventDistributionPoint(int eventCacheSize)
        {
            _eventCacheSize = eventCacheSize;
        }

        public bool Handle(ProjectionMessage.Projections.CommittedEventReceived message)
        {
            if (message.CorrelationId != _distributionPointId)
                return false;
            if (message.Data == null)
                return true; 
            CacheRecentMessage(message);
            DistributeMessage(message);
            if (_headSubscribers.Count == 0 && !_headDistributionPointPaused)
            {
                _headDistributionPoint.Pause();
                _headDistributionPointPaused = true;
            }
            return true;
        }

        public void Start(Guid distributionPointId, TransactionFileReaderEventDistributionPoint transactionFileReaderEventDistributionPoint)
        {
            _distributionPointId = distributionPointId;
            _headDistributionPoint = transactionFileReaderEventDistributionPoint;
            //Guid.Empty means head distribution point
            _headDistributionPoint.Resume();
        }

        public void Stop()
        {
            _headDistributionPoint = null;
        }

        public bool TrySubscribe(Guid projectionId, ProjectionSubscription projectionSubscription, CheckpointTag fromCheckpointTag)
        {
            if (projectionSubscription.CanJoinAt(_subscribeFromPosition, fromCheckpointTag))
            {
                DispatchRecentMessagesTo(projectionSubscription);
                AddSubscriber(projectionId, projectionSubscription);
                return true;
            }
            return false;
        }

        public void Unsubscribe(Guid projectionId)
        {
            _headSubscribers.Remove(projectionId);
        }

        private void DispatchRecentMessagesTo(ProjectionSubscription subscription)
        {
            foreach (var m in _lastMessages)
                subscription.Handle(m);
        }

        private void DistributeMessage(ProjectionMessage.Projections.CommittedEventReceived message)
        {
            foreach (var subscriber in _headSubscribers.Values)
                subscriber.Handle(message);
        }

        private void CacheRecentMessage(ProjectionMessage.Projections.CommittedEventReceived message)
        {
            _lastMessages.Enqueue(message);
            if (_lastMessages.Count > _eventCacheSize)
            {
                _lastMessages.Dequeue();
            }
            var lastAvailableCommittedevent = _lastMessages.Peek();
            _subscribeFromPosition = lastAvailableCommittedevent.Position;
        }

        private void AddSubscriber(Guid publishWithCorrelationId, ProjectionSubscription subscription)
        {
            _headSubscribers.Add(publishWithCorrelationId, subscription);
            if (_headDistributionPointPaused)
            {
                _headDistributionPointPaused = false;
                _headDistributionPoint.Resume();
            }
        }
    }
}