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
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class ProjectionSubscriptionBase
    {
        private readonly ILogger _logger = LogManager.GetLoggerFor<EventReorderingReaderSubscription>();
        private readonly IPublisher _publisher;
        private readonly IReaderStrategy _readerStrategy;
        private readonly long? _checkpointUnhandledBytesThreshold;
        private readonly int? _checkpointProcessedEventsThreshold;
        private readonly bool _stopOnEof;
        private readonly int? _stopAfterNEvents;
        private readonly EventFilter _eventFilter;
        private readonly PositionTagger _positionTagger;
        private readonly PositionTracker _positionTracker;
        private long? _lastPassedOrCheckpointedEventPosition;
        private float _progress = -1;
        private long _subscriptionMessageSequenceNumber;
        private int _eventsSinceLastCheckpointSuggested;
        private readonly Guid _subscriptionId;
        private bool _eofReached;

        protected ProjectionSubscriptionBase(
            IPublisher publisher, Guid subscriptionId, CheckpointTag @from, IReaderStrategy readerStrategy,
            long? checkpointUnhandledBytesThreshold, int? checkpointProcessedEventsThreshold, bool stopOnEof,
            int? stopAfterNEvents)
        {
            if (publisher == null) throw new ArgumentNullException("publisher");
            if (readerStrategy == null) throw new ArgumentNullException("readerStrategy");
            _publisher = publisher;
            _readerStrategy = readerStrategy;
            _checkpointUnhandledBytesThreshold = checkpointUnhandledBytesThreshold;
            _checkpointProcessedEventsThreshold = checkpointProcessedEventsThreshold;
            _stopOnEof = stopOnEof;
            _stopAfterNEvents = stopAfterNEvents;
            _subscriptionId = subscriptionId;
            _lastPassedOrCheckpointedEventPosition = null;

            _eventFilter = readerStrategy.EventFilter;

            _positionTagger = readerStrategy.PositionTagger;
            _positionTracker = new PositionTracker(_positionTagger);
            _positionTracker.UpdateByCheckpointTagInitial(@from);
        }

        protected void ProcessOne(ReaderSubscriptionMessage.CommittedEventDistributed message)
        {
            // NOTE: we may receive here messages from heading event distribution point 
            // and they may not pass out source filter.  Discard them first
            var roundedProgress = (float) Math.Round(message.Progress, 2);
            bool progressChanged = _progress != roundedProgress;
            _progress = roundedProgress;
            if (
                !_eventFilter.PassesSource(
                    message.Data.ResolvedLinkTo, message.Data.PositionStreamId, message.Data.EventType))
            {
                if (progressChanged)
                    PublishProgress();
                return;
            }

            // NOTE: after joining heading distribution point it delivers all cached events to the subscription
            // some of this events we may have already received. The delivered events may have different order 
            // (in case of partially ordered cases multi-stream reader etc). We discard all the messages that are not 
            // after the last available checkpoint tag

            //NOTE: older events can appear here when replaying events from the heading event reader
            //      or when event-by-type-index reader reads TF and both event and resolved-event appear as output

            if (!_positionTagger.IsMessageAfterCheckpointTag(_positionTracker.LastTag, message))
            {

/*
                _logger.Trace(
                    "Skipping replayed event {0}@{1} at position {2}. the last processed event checkpoint tag is: {3}",
                    message.PositionSequenceNumber, message.PositionStreamId, message.Position, _positionTracker.LastTag);
*/
                return;
            }
            var eventCheckpointTag = _positionTagger.MakeCheckpointTag(_positionTracker.LastTag, message);
            _positionTracker.UpdateByCheckpointTagForward(eventCheckpointTag);
            if (_eventFilter.Passes(message.Data.ResolvedLinkTo, message.Data.PositionStreamId, message.Data.EventType))
            {
                _lastPassedOrCheckpointedEventPosition = message.Data.Position.PreparePosition;
                var convertedMessage =
                    EventReaderSubscriptionMessage.CommittedEventReceived.FromCommittedEventDistributed(
                        message, eventCheckpointTag, _eventFilter.GetCategory(message.Data.PositionStreamId),
                        _subscriptionId, _subscriptionMessageSequenceNumber++);
                _publisher.Publish(convertedMessage);
                _eventsSinceLastCheckpointSuggested++;
                if (_checkpointProcessedEventsThreshold > 0
                    && _eventsSinceLastCheckpointSuggested >= _checkpointProcessedEventsThreshold)
                    SuggestCheckpoint(message);
            }
            else
            {
                if (_checkpointUnhandledBytesThreshold > 0
                    && (_lastPassedOrCheckpointedEventPosition != null
                        && message.Data.Position.PreparePosition - _lastPassedOrCheckpointedEventPosition.Value
                        > _checkpointUnhandledBytesThreshold))
                {
                    SuggestCheckpoint(message);
                }
                else
                {
                    if (progressChanged)
                        PublishProgress();
                }
            }
            // initialize checkpointing based on first message 
            if (_lastPassedOrCheckpointedEventPosition == null)
                _lastPassedOrCheckpointedEventPosition = message.Data.Position.PreparePosition;
        }

        private void PublishProgress()
        {
            _publisher.Publish(
                new EventReaderSubscriptionMessage.ProgressChanged(
                    _subscriptionId, _positionTracker.LastTag, _progress, _subscriptionMessageSequenceNumber++));
        }

        protected void PublishStartingAt(long startingLastCommitPosition)
        {
            _publisher.Publish(
                new EventReaderSubscriptionMessage.SubscriptionStarted(
                    _subscriptionId, _positionTracker.LastTag, startingLastCommitPosition,
                    _subscriptionMessageSequenceNumber++));
        }

        private void PublishNotAuthorized()
        {
            _publisher.Publish(
                new EventReaderSubscriptionMessage.NotAuthorized(
                    _subscriptionId, _positionTracker.LastTag, _progress, _subscriptionMessageSequenceNumber++));
        }

        private void SuggestCheckpoint(ReaderSubscriptionMessage.CommittedEventDistributed message)
        {
            _lastPassedOrCheckpointedEventPosition = message.Data.Position.PreparePosition;
            _publisher.Publish(
                new EventReaderSubscriptionMessage.CheckpointSuggested(
                    _subscriptionId, _positionTracker.LastTag, message.Progress,
                    _subscriptionMessageSequenceNumber++));
            _eventsSinceLastCheckpointSuggested = 0;
        }

        public IEventReader CreatePausedEventReader(IPublisher publisher, IODispatcher ioDispatcher, Guid eventReaderId)
        {
            if (_eofReached)
                throw new InvalidOperationException("Onetime projection has already reached the eof position");
            _logger.Trace("Creating an event distribution point at '{0}'", _positionTracker.LastTag);
            return _readerStrategy.CreatePausedEventReader(
                eventReaderId, publisher, ioDispatcher, _positionTracker.LastTag, _stopOnEof, _stopAfterNEvents);
        }

        public void Handle(ReaderSubscriptionMessage.EventReaderEof message)
        {
            if (_stopOnEof)
            {
                _eofReached = true;
                EofReached();
                _publisher.Publish(
                    new EventReaderSubscriptionMessage.EofReached(
                        _subscriptionId, _positionTracker.LastTag,
                        _subscriptionMessageSequenceNumber++));
            }
        }

        public void Handle(ReaderSubscriptionMessage.EventReaderPartitionEof message)
        {
            var eventCheckpointTag = _positionTagger.MakeCheckpointTag(_positionTracker.LastTag, message);
            
            _publisher.Publish(
                new EventReaderSubscriptionMessage.PartitionEofReached(
                    _subscriptionId, eventCheckpointTag, message.Partition,
                    _subscriptionMessageSequenceNumber++));
        }

        public void Handle(ReaderSubscriptionMessage.EventReaderPartitionMeasured message)
        {
            _publisher.Publish(
                            new EventReaderSubscriptionMessage.PartitionMeasured(
                                _subscriptionId, message.Partition, message.Size,
                                _subscriptionMessageSequenceNumber++));
        }

        public void Handle(ReaderSubscriptionMessage.EventReaderNotAuthorized message)
        {
            if (_stopOnEof)
            {
                _eofReached = true;
                _publisher.Publish(
                    new EventReaderSubscriptionMessage.NotAuthorized(
                        _subscriptionId, _positionTracker.LastTag, _progress, _subscriptionMessageSequenceNumber++));
            }
        }

        protected virtual void EofReached()
        {
        }

        public void Handle(ReaderSubscriptionMessage.EventReaderStarting message)
        {
            PublishStartingAt(message.LastCommitPosition);
        }
    }
}
