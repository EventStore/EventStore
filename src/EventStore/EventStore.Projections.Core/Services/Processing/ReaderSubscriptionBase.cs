using System;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class ReaderSubscriptionBase
    {
        private readonly ILogger _logger = LogManager.GetLoggerFor<EventReorderingReaderSubscription>();
        private readonly IPublisher _publisher;
        private readonly IReaderStrategy _readerStrategy;
        private readonly long? _checkpointUnhandledBytesThreshold;
        private readonly int? _checkpointProcessedEventsThreshold;
        private readonly bool _stopOnEof;
        private readonly int? _stopAfterNEvents;
        protected readonly EventFilter _eventFilter;
        protected readonly PositionTagger _positionTagger;
        protected readonly PositionTracker _positionTracker;
        private long? _lastPassedOrCheckpointedEventPosition;
        private float _progress = -1;
        private long _subscriptionMessageSequenceNumber;
        private int _eventsSinceLastCheckpointSuggested;
        private readonly Guid _subscriptionId;
        private bool _eofReached;
        protected string _tag;

        protected ReaderSubscriptionBase(
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

        public string Tag
        {
            get { return _tag; }
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
            if (_eventFilter.Passes(
                message.Data.ResolvedLinkTo, message.Data.PositionStreamId, message.Data.EventType,
                message.Data.IsStreamDeletedEvent))
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

        protected void PublishPartitionDeleted(string partition, CheckpointTag deletePosition)
        {
            _publisher.Publish(
                new EventReaderSubscriptionMessage.PartitionDeleted(
                    _subscriptionId, deletePosition, partition, _subscriptionMessageSequenceNumber++));
        }

        private void PublishStartingAt(long startingLastCommitPosition)
        {
            _publisher.Publish(
                new EventReaderSubscriptionMessage.SubscriptionStarted(
                    _subscriptionId, _positionTracker.LastTag, startingLastCommitPosition,
                    _subscriptionMessageSequenceNumber++));
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

        public void Handle(ReaderSubscriptionMessage.EventReaderStarting message)
        {
            PublishStartingAt(message.LastCommitPosition);
        }

        protected virtual void EofReached()
        {
        }
    }
}
