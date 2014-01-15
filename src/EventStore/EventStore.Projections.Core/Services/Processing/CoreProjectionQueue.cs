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
using System.Net.Configuration;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class CoreProjectionQueue
    {
        private readonly ILogger _logger = LogManager.GetLoggerFor<CoreProjectionQueue>();
        private readonly StagedProcessingQueue _queuePendingEvents;

        private readonly IPublisher _publisher;
        private readonly Guid _projectionCorrelationId;
        private readonly int _pendingEventsThreshold;
        private readonly Action _updateStatistics;

        private CheckpointTag _lastEnqueuedEventTag;
        private bool _justInitialized;
        private bool _subscriptionPaused;

        public event Action EnsureTickPending
        {
            add { _queuePendingEvents.EnsureTickPending += value; }
            remove { _queuePendingEvents.EnsureTickPending -= value; }
        }

        public CoreProjectionQueue(
            Guid projectionCorrelationId, IPublisher publisher, int pendingEventsThreshold,
            bool orderedPartitionProcessing, Action updateStatistics = null)
        {
            _queuePendingEvents =
                new StagedProcessingQueue(
                    new[]
                    {
                        true /* record event order - async with ordered output*/, 
                        true /* get state partition - ordered as it may change correlation id - sync */, 
                        false /* load foreach state - async- unordered completion*/, 
                        orderedPartitionProcessing /* process Js - unordered/ordered - inherently unordered/ordered completion*/, 
                        true /* write emits - ordered - async ordered completion*/, 
                        false /* complete item */ 
                    });
            _publisher = publisher;
            _projectionCorrelationId = projectionCorrelationId;
            _pendingEventsThreshold = pendingEventsThreshold;
            _updateStatistics = updateStatistics;
        }

        public bool IsRunning
        {
            get { return _isRunning; }
        }

        public bool ProcessEvent()
        {
            var processed = false;
            if (_queuePendingEvents.Count > 0)
                processed = ProcessOneEventBatch();
            return processed;
        }

        public int GetBufferedEventCount()
        {
            return _queuePendingEvents.Count;
        }

        public void EnqueueTask(WorkItem workItem, CheckpointTag workItemCheckpointTag, bool allowCurrentPosition = false)
        {
            ValidateQueueingOrder(workItemCheckpointTag, allowCurrentPosition);
            workItem.SetProjectionQueue(this);
            workItem.SetCheckpointTag(workItemCheckpointTag);
            _queuePendingEvents.Enqueue(workItem);
        }

        public void EnqueueOutOfOrderTask(WorkItem workItem)
        {
            if (_lastEnqueuedEventTag == null)
                throw new InvalidOperationException(
                    "Cannot enqueue an out-of-order task.  The projection position is currently unknown.");
            workItem.SetProjectionQueue(this);
            workItem.SetCheckpointTag(_lastEnqueuedEventTag);
            _queuePendingEvents.Enqueue(workItem);
        }

        public void InitializeQueue(CheckpointTag zeroCheckpointTag)
        {
            _subscriptionPaused = false;
            _unsubscribed = false;
            _lastReportedStatisticsTimeStamp = default(DateTime);
            _unsubscribed = false;
            _subscriptionId = Guid.Empty;

            _queuePendingEvents.Initialize();

            _lastEnqueuedEventTag = zeroCheckpointTag;
            _justInitialized = true;
        }

        public string GetStatus()
        {
            return (_subscriptionPaused ? "/Paused" : "");
        }

        private void ValidateQueueingOrder(CheckpointTag eventTag, bool allowCurrentPosition = false)
        {
            if (eventTag < _lastEnqueuedEventTag || (!(allowCurrentPosition || _justInitialized) && eventTag <= _lastEnqueuedEventTag))
                throw new InvalidOperationException(
                    string.Format(
                        "Invalid order.  Last known tag is: '{0}'.  Current tag is: '{1}'", _lastEnqueuedEventTag,
                        eventTag));
            _justInitialized = _justInitialized && (eventTag == _lastEnqueuedEventTag);
            _lastEnqueuedEventTag = eventTag;
        }

        private void PauseSubscription()
        {
            if (_subscriptionId == Guid.Empty)
                throw new InvalidOperationException("Not subscribed");
            if (!_subscriptionPaused && !_unsubscribed)
            {
                _subscriptionPaused = true;
                _publisher.Publish(
                    new ReaderSubscriptionManagement.Pause(_subscriptionId));
            }
        }

        private void ResumeSubscription()
        {
            if (_subscriptionId == Guid.Empty)
                throw new InvalidOperationException("Not subscribed");
            if (_subscriptionPaused && !_unsubscribed)
            {
                _subscriptionPaused = false;
                _publisher.Publish(
                    new ReaderSubscriptionManagement.Resume(_subscriptionId));
            }
        }

        private DateTime _lastReportedStatisticsTimeStamp = default(DateTime);
        private bool _unsubscribed;
        private Guid _subscriptionId;
        private bool _isRunning;

        private bool ProcessOneEventBatch()
        {
            if (_queuePendingEvents.Count > _pendingEventsThreshold)
                PauseSubscription();
            var processed = _queuePendingEvents.Process(max: 30);
            if (_subscriptionPaused && _queuePendingEvents.Count < _pendingEventsThreshold / 2)
                ResumeSubscription();

            if (_updateStatistics != null
                && ((_queuePendingEvents.Count == 0)
                    || (DateTime.UtcNow - _lastReportedStatisticsTimeStamp).TotalMilliseconds > 500))
                _updateStatistics();
            _lastReportedStatisticsTimeStamp = DateTime.UtcNow;
            return processed;
        }

        public void Unsubscribed()
        {
            _unsubscribed = true;
        }

        public void Subscribed(Guid currentSubscriptionId)
        {
            if (_unsubscribed)
                throw new InvalidOperationException("Unsubscribed");
            if (_subscriptionId != Guid.Empty)
                throw new InvalidOperationException("Already subscribed");
            _subscriptionId = currentSubscriptionId;
        }

        public void SetIsRunning(bool isRunning)
        {
            _isRunning = isRunning;
        }
    }
}
