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
using EventStore.Core.Bus;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class CoreProjectionQueue
    {
        private readonly StagedProcessingQueue _queuePendingEvents =
            new StagedProcessingQueue(
                new[] {false /* load foreach state */, false /* process Js */, true /* write emits */});

        private readonly IPublisher _publisher;
        private readonly Guid _projectionCorrelationId;
        private readonly int _pendingEventsThreshold;
        private readonly Action _updateStatistics;

        private QueueState _queueState;
        private CheckpointTag _lastEnqueuedEventTag;
        private bool _subscriptionPaused;

        public CoreProjectionQueue(
            Guid projectionCorrelationId, IPublisher publisher, int pendingEventsThreshold,
            Action updateStatistics = null)
        {
            _publisher = publisher;
            _projectionCorrelationId = projectionCorrelationId;
            _pendingEventsThreshold = pendingEventsThreshold;
            _updateStatistics = updateStatistics;
        }

        public void Initialize()
        {
            _queueState = default(QueueState);
            _lastEnqueuedEventTag = default(CheckpointTag);
            _subscriptionPaused = false;

            _queuePendingEvents.Initialize();
        }

        public void ProcessEvent()
        {
            if (_queueState == QueueState.Running)
                if (_queuePendingEvents.Count > 0)
                    ProcessOneEvent();
        }

        public int GetBufferedEventCount()
        {
            return _queuePendingEvents.Count;
        }

        public void SetRunning()
        {
            _queueState = QueueState.Running;
            ResumeSubscription();
        }

        public void SetPaused()
        {
            _queueState = QueueState.Paused;
            PauseSubscription();
        }

        public void SetStopped()
        {
            _queueState = QueueState.Stopped;
            // unsubscribe?
        }

        public void EnqueueTask(WorkItem workItem, CheckpointTag workItemCheckpointTag, bool allowCurrentPosition = false)
        {
            if (_queueState == QueueState.Stopped)
                throw new InvalidOperationException("Queue is Stopped");
            ValidateQueueingOrder(workItemCheckpointTag, allowCurrentPosition);
            workItem.SetCheckpointTag(workItemCheckpointTag);
            _queuePendingEvents.Enqueue(workItem);
        }

        public void EnqueueOutOfOrderTask(WorkItem workItem)
        {
            workItem.SetCheckpointTag(_lastEnqueuedEventTag);
            _queuePendingEvents.Enqueue(workItem);
        }

        public void InitializeQueue(CheckpointTag zeroCheckpointTag)
        {
            _lastEnqueuedEventTag = zeroCheckpointTag;
        }

        public string GetStatus()
        {
            return (_subscriptionPaused && _queueState != QueueState.Paused ? "/Subscription Paused" : "");
        }

        private void ValidateQueueingOrder(CheckpointTag eventTag, bool allowCurrentPosition = false)
        {
            if (eventTag < _lastEnqueuedEventTag || (!allowCurrentPosition && eventTag <= _lastEnqueuedEventTag))
                throw new InvalidOperationException(
                    string.Format(
                        "Invalid order.  Last known tag is: '{0}'.  Current tag is: '{1}'", _lastEnqueuedEventTag,
                        eventTag));
            _lastEnqueuedEventTag = eventTag;
        }

        private void PauseSubscription()
        {
            if (!_subscriptionPaused)
            {
                _subscriptionPaused = true;
                _publisher.Publish(
                    new ProjectionSubscriptionManagement.Pause(_projectionCorrelationId));
            }
        }

        private void ResumeSubscription()
        {
            if (_subscriptionPaused && _queueState == QueueState.Running)
            {
                _subscriptionPaused = false;
                _publisher.Publish(
                    new ProjectionSubscriptionManagement.Resume(_projectionCorrelationId));
            }
        }

        private DateTime _lastReportedStatisticsTimeStamp = default(DateTime);

        private void ProcessOneEvent()
        {
            int pendingEventsCount = _queuePendingEvents.Count;
            if (pendingEventsCount > _pendingEventsThreshold)
                PauseSubscription();
            if (_subscriptionPaused && pendingEventsCount < _pendingEventsThreshold/2)
                ResumeSubscription();
            _queuePendingEvents.Process();

            if (_updateStatistics != null
                && ((_queuePendingEvents.Count == 0)
                    || (DateTime.UtcNow - _lastReportedStatisticsTimeStamp).TotalMilliseconds > 500))
                _updateStatistics();
            _lastReportedStatisticsTimeStamp = DateTime.UtcNow;
        }

        private enum QueueState
        {
            Stopped,
            Paused,
            Running
        }
    }
}
