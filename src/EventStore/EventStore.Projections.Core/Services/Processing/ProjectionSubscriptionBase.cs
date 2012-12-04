using System;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class ProjectionSubscriptionBase
    {
        private readonly ILogger _logger = LogManager.GetLoggerFor<EventReorderingProjectionSubscription>();
        private readonly Guid _projectionCorrelationId;
        private readonly IHandle<ProjectionSubscriptionMessage.CommittedEventReceived> _eventHandler;
        private readonly IHandle<ProjectionSubscriptionMessage.CheckpointSuggested> _checkpointHandler;
        private readonly IHandle<ProjectionSubscriptionMessage.ProgressChanged> _progressHandler;
        private readonly IHandle<ProjectionSubscriptionMessage.EofReached> _eofHandler;
        private readonly CheckpointStrategy _checkpointStrategy;
        private readonly long? _checkpointUnhandledBytesThreshold;
        private readonly bool _stopOnEof;
        private readonly EventFilter _eventFilter;
        private readonly PositionTagger _positionTagger;
        private readonly PositionTracker _positionTracker;
        private long? _lastPassedOrCheckpointedEventPosition;
        private float _progress = -1;
        private long _subscriptionMessageSequenceNumber;
        private bool _eofReached;

        protected ProjectionSubscriptionBase(
            Guid projectionCorrelationId, CheckpointTag from,
            IHandle<ProjectionSubscriptionMessage.CommittedEventReceived> eventHandler,
            IHandle<ProjectionSubscriptionMessage.CheckpointSuggested> checkpointHandler,
            IHandle<ProjectionSubscriptionMessage.ProgressChanged> progressHandler,
            IHandle<ProjectionSubscriptionMessage.EofReached> eofHandler,
            CheckpointStrategy checkpointStrategy, long? checkpointUnhandledBytesThreshold, bool stopOnEof)
        {
            if (eventHandler == null) throw new ArgumentNullException("eventHandler");
            if (checkpointHandler == null) throw new ArgumentNullException("checkpointHandler");
            if (progressHandler == null) throw new ArgumentNullException("progressHandler");
            if (eofHandler == null) throw new ArgumentNullException("eofHandler");
            if (checkpointStrategy == null) throw new ArgumentNullException("checkpointStrategy");
            _eventHandler = eventHandler;
            _checkpointHandler = checkpointHandler;
            _progressHandler = progressHandler;
            _eofHandler = eofHandler;
            _checkpointStrategy = checkpointStrategy;
            _checkpointUnhandledBytesThreshold = checkpointUnhandledBytesThreshold;
            _stopOnEof = stopOnEof;
            _projectionCorrelationId = projectionCorrelationId;
            _lastPassedOrCheckpointedEventPosition = null;

            _eventFilter = checkpointStrategy.EventFilter;

            _positionTagger = checkpointStrategy.PositionTagger;
            _positionTracker = new PositionTracker(_positionTagger);
            _positionTracker.UpdateByCheckpointTagInitial(@from);
        }

        protected void ProcessOne(ProjectionCoreServiceMessage.CommittedEventDistributed message)
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

        public EventReader CreatePausedEventReader(IPublisher publisher, Guid eventReaderId)
        {
            if (_eofReached)
                throw new InvalidOperationException("Onetime projection has alerady reached the eof position");
            _logger.Trace("Creating an event distribution point at '{0}'", _positionTracker.LastTag);
            return _checkpointStrategy.CreatePausedEventReader(
                eventReaderId, publisher, _positionTracker.LastTag, _stopOnEof);
        }

        public void Handle(ProjectionCoreServiceMessage.EventReaderEof message)
        {
            if (_stopOnEof)
            {
                _eofReached = true;
                EofReached();
                _eofHandler.Handle(
                    new ProjectionSubscriptionMessage.EofReached(
                        _projectionCorrelationId, _positionTracker.LastTag, _progress,
                        _subscriptionMessageSequenceNumber++));
            }
        }

        protected virtual void EofReached()
        {
        }
    }
}