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

        private readonly CheckpointStrategy _checkpointStrategy;
        private readonly IPublisher _publisher;
        private readonly Guid _projectionCorrelationId;
        private readonly Action _tick;

        private QueueState _queueState;
        private CheckpointTag _lastEnqueuedEventTag;
        private bool _subscriptionPaused;
        private bool _tickPending;
        private readonly int _pendingEventsThreshold;
        private readonly bool _checkpointsEnabled;

        public CoreProjectionQueue(Guid projectionCorrelationId, IPublisher publisher, CheckpointStrategy checkpointStrategy, Action tick, bool checkpointsEnabled, int pendingEventsThreshold)
        {
            _checkpointStrategy = checkpointStrategy;
            _publisher = publisher;
            _projectionCorrelationId = projectionCorrelationId;
            _tick = tick;
            _pendingEventsThreshold = pendingEventsThreshold;
            _checkpointsEnabled = checkpointsEnabled;
        }

        public void ProcessEvent()
        {
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
        }

        public void SetPaused()
        {
            _queueState = QueueState.Running;
        }

        public void SetStopped()
        {
            _queueState = QueueState.Running;
        }

        public void QueueTick()
        {
            _tickPending = false;
            if (_queueState == QueueState.Running)
                ProcessEvent();
        }

        public void HandleCommittedEventReceived(
            CoreProjection coreProjection, ProjectionMessage.Projections.CommittedEventReceived message)
        {
            if (_queueState == QueueState.Stopped)
                return;
            CheckpointTag eventTag = _checkpointStrategy.PositionTagger.MakeCheckpointTag(message);
            ValidateQueueingOrder(eventTag);
            string partition = _checkpointStrategy.StatePartitionSelector.GetStatePartition(message);
            _queuePendingEvents.Enqueue(
                new CoreProjection.CommittedEventWorkItem(coreProjection, message, partition, eventTag));
            if (_queueState == QueueState.Running)
                ProcessOneEvent();
        }

        public void HandleCheckpointSuggested(
            CoreProjection coreProjection, ProjectionMessage.Projections.CheckpointSuggested message)
        {
            if (_queueState == QueueState.Stopped)
                return;
            if (_checkpointsEnabled)
            {
                CheckpointTag checkpointTag = message.CheckpointTag;
                ValidateQueueingOrder(checkpointTag);
                _queuePendingEvents.Enqueue(
                    new CoreProjection.CheckpointSuggestedWorkItem(coreProjection, message, checkpointTag));
            }
            if (_queueState == QueueState.Running)
                ProcessOneEvent();
        }

        public void InitializeQueue()
        {
            _lastEnqueuedEventTag = _checkpointStrategy.PositionTagger.MakeZeroCheckpointTag();
        }

        public string GetStatus()
        {
            return (_subscriptionPaused && _queueState != QueueState.Paused ? "/Subscription Paused" : "");
        }

        private void ValidateQueueingOrder(CheckpointTag eventTag)
        {
            if (eventTag <= _lastEnqueuedEventTag)
                throw new InvalidOperationException("Invalid order.  Last known tag is: '{0}'.  Current tag is: '{1}'");
            _lastEnqueuedEventTag = eventTag;
        }

        internal void PauseSubscription()
        {
            if (!_subscriptionPaused)
            {
                _subscriptionPaused = true;
                _publisher.Publish(
                    new ProjectionMessage.Projections.PauseProjectionSubscription(_projectionCorrelationId));
            }
        }

        internal void ResumeSubscription()
        {
            if (_subscriptionPaused && _queueState == QueueState.Running)
            {
                _subscriptionPaused = false;
                _publisher.Publish(
                    new ProjectionMessage.Projections.ResumeProjectionSubscription(_projectionCorrelationId));
            }
        }

        internal void EnsureTickPending()
        {
            if (_tickPending)
                return;
            if (_queueState == QueueState.Paused)
                return;
            _tickPending = true;
            _publisher.Publish(new ProjectionMessage.CoreService.Tick(_tick));
        }

        private void ProcessOneEvent()
        {
            int pendingEventsCount = _queuePendingEvents.Count;
            if (pendingEventsCount > _pendingEventsThreshold)
                PauseSubscription();
            if (_subscriptionPaused && pendingEventsCount < _pendingEventsThreshold/2)
                ResumeSubscription();
            int processed = _queuePendingEvents.Process();
            if (processed > 0)
                EnsureTickPending();
        }

        private enum QueueState
        {
            Stopped,
            Paused,
            Running
        }
    }
}
