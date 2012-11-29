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
using System.Linq;

namespace EventStore.Projections.Core.Services.Processing
{
    public class EventReorderingProjectionSubscription : IProjectionSubscription
    {
        private readonly ILogger _logger = LogManager.GetLoggerFor<EventReorderingProjectionSubscription>();
        private readonly Guid _projectionCorrelationId;
        private readonly IHandle<ProjectionSubscriptionMessage.CommittedEventReceived> _eventHandler;
        private readonly IHandle<ProjectionSubscriptionMessage.CheckpointSuggested> _checkpointHandler;
        private readonly IHandle<ProjectionSubscriptionMessage.ProgressChanged> _progressHandler;
        private readonly CheckpointStrategy _checkpointStrategy;
        private readonly long? _checkpointUnhandledBytesThreshold;
        private readonly EventFilter _eventFilter;
        private readonly PositionTagger _positionTagger;
        private readonly PositionTracker _positionTracker;
        private readonly SortedList<long, ProjectionCoreServiceMessage.CommittedEventDistributed> _buffer =
            new SortedList<long, ProjectionCoreServiceMessage.CommittedEventDistributed>();
        private long? _lastPassedOrCheckpointedEventPosition;
        private float _progress = -1;
        private long _subscriptionMessageSequenceNumber;
        private readonly int _processingLagMs;

        public EventReorderingProjectionSubscription(
            Guid projectionCorrelationId, CheckpointTag from,
            IHandle<ProjectionSubscriptionMessage.CommittedEventReceived> eventHandler,
            IHandle<ProjectionSubscriptionMessage.CheckpointSuggested> checkpointHandler,
            IHandle<ProjectionSubscriptionMessage.ProgressChanged> progressHandler,
            CheckpointStrategy checkpointStrategy, long? checkpointUnhandledBytesThreshold, int processingLagMs)
        {
            if (eventHandler == null) throw new ArgumentNullException("eventHandler");
            if (checkpointHandler == null) throw new ArgumentNullException("checkpointHandler");
            if (progressHandler == null) throw new ArgumentNullException("progressHandler");
            if (checkpointStrategy == null) throw new ArgumentNullException("checkpointStrategy");
            _eventHandler = eventHandler;
            _checkpointHandler = checkpointHandler;
            _progressHandler = progressHandler;
            _checkpointStrategy = checkpointStrategy;
            _checkpointUnhandledBytesThreshold = checkpointUnhandledBytesThreshold;
            _processingLagMs = processingLagMs;
            _projectionCorrelationId = projectionCorrelationId;
            _lastPassedOrCheckpointedEventPosition = null;

            _eventFilter = checkpointStrategy.EventFilter;

            _positionTagger = checkpointStrategy.PositionTagger;
            _positionTracker = new PositionTracker(_positionTagger);
            _positionTracker.UpdateByCheckpointTagInitial(@from);
        }

        public void Handle(ProjectionCoreServiceMessage.CommittedEventDistributed message)
        {
            if (message.Data == null)
                throw new NotSupportedException();
            ProjectionCoreServiceMessage.CommittedEventDistributed existing;
            // ignore duplicate messages (when replaying from heading event distribution point)
            if (!_buffer.TryGetValue(message.Position.PreparePosition, out existing))
            {
                _buffer.Add(message.Position.PreparePosition, message);
                var maxTimestamp = _buffer.Max(v => v.Value.Data.Timestamp);
                ProcessAllFor(maxTimestamp);
            }
        }

        private void ProcessAllFor(DateTime maxTimestamp)
        {

            if (_buffer.Count == 0)
                throw new InvalidOperationException();

            //NOTE: this is the most straightforward implementation 
            //TODO: build proper data structure when the approach is finalized
            bool processed;
            do
            {
                processed = ProcessFor(maxTimestamp);
            } while (processed);
        }

        private bool ProcessFor(DateTime maxTimestamp)
        {
            if (_buffer.Count == 0)
                return false;
            var first = _buffer.ElementAt(0);
            if ((maxTimestamp - first.Value.Data.Timestamp).TotalMilliseconds > _processingLagMs)
            {
                _buffer.RemoveAt(0);
                ProcessOne(first.Value);
                return true;
            }
            return false;
        }

        private void ProcessOne(ProjectionCoreServiceMessage.CommittedEventDistributed message)
        {
            // NOTE: we may receive here messages from heading event distribution point 
            // and they may not pass out source filter.  Discard them first
            var roundedProgress = (float) Math.Round(message.Progress, 2);
            bool progressChanged = _progress != roundedProgress;
            _progress = roundedProgress;
            if (!_eventFilter.PassesSource(message.ResolvedLinkTo, message.PositionStreamId))
            {
                if (progressChanged)
                    _progressHandler.Handle(
                        new ProjectionSubscriptionMessage.ProgressChanged(
                            _projectionCorrelationId, _positionTracker.LastTag, _progress,
                            _subscriptionMessageSequenceNumber++));
                return;
            }
            // NOTE: after joining heading distribution point it delivers all cached events to the subscription
            // some of this events we may have already received. The delivered events may have different order 
            // (in case of partially ordered cases multi-stream reader etc). We discard all the messages that are not 
            // after the last available checkpoint tag
            if (!_positionTagger.IsMessageAfterCheckpointTag(_positionTracker.LastTag, message))
            {
                _logger.Trace(
                    "Skipping replayed event {0}@{1} at position {2}. the last processed event checkpoint tag is: {3}",
                    message.PositionSequenceNumber, message.PositionStreamId, message.Position, _positionTracker.LastTag);
                return;
            }
            var eventCheckpointTag = _positionTagger.MakeCheckpointTag(_positionTracker.LastTag, message);
            if (eventCheckpointTag <= _positionTracker.LastTag)
                throw new Exception(
                    string.Format(
                        "Invalid checkpoint tag was built.  Tag '{0}' must be greater than '{1}'", eventCheckpointTag,
                        _positionTracker.LastTag));
            _positionTracker.UpdateByCheckpointTagForward(eventCheckpointTag);
            if (_eventFilter.Passes(message.ResolvedLinkTo, message.PositionStreamId, message.Data.EventType))
            {
                _lastPassedOrCheckpointedEventPosition = message.Position.PreparePosition;
                var convertedMessage =
                    ProjectionSubscriptionMessage.CommittedEventReceived.FromCommittedEventDistributed(
                        message, eventCheckpointTag, _eventFilter.GetCategory(message.PositionStreamId),
                        _subscriptionMessageSequenceNumber++);
                _eventHandler.Handle(convertedMessage);
            }
            else
            {
                if (_checkpointUnhandledBytesThreshold != null
                    && (_lastPassedOrCheckpointedEventPosition != null
                        && message.Position.PreparePosition - _lastPassedOrCheckpointedEventPosition.Value
                        > _checkpointUnhandledBytesThreshold))
                {
                    _lastPassedOrCheckpointedEventPosition = message.Position.PreparePosition;
                    _checkpointHandler.Handle(
                        new ProjectionSubscriptionMessage.CheckpointSuggested(
                            _projectionCorrelationId, _positionTracker.LastTag, message.Progress,
                            _subscriptionMessageSequenceNumber++));
                }
                else
                {
                    if (progressChanged)
                        _progressHandler.Handle(
                            new ProjectionSubscriptionMessage.ProgressChanged(
                                _projectionCorrelationId, _positionTracker.LastTag, _progress,
                                _subscriptionMessageSequenceNumber++));
                }
            }
            // initialize checkpointing based on first message 
            if (_lastPassedOrCheckpointedEventPosition == null)
                _lastPassedOrCheckpointedEventPosition = message.Position.PreparePosition;
        }

        public void Handle(ProjectionCoreServiceMessage.EventDistributionPointIdle message)
        {
            ProcessAllFor(message.IdleTimestampUtc);
        }

        public EventDistributionPoint CreatePausedEventDistributionPoint(IPublisher publisher, Guid distributionPointId)
        {
            _logger.Trace("Creating an event distribution point at '{0}'", _positionTracker.LastTag);
            return _checkpointStrategy.CreatePausedEventDistributionPoint(
                distributionPointId, publisher, _positionTracker.LastTag);
        }
    }
}
