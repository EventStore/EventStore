using System;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Core.Services.TimerService;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing {
	public class ReaderSubscriptionBase {
		private readonly IPublisher _publisher;
		private readonly IReaderStrategy _readerStrategy;
		private readonly ITimeProvider _timeProvider;
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
		private int _eventsSinceLastCheckpointSuggestedOrStart;
		private readonly Guid _subscriptionId;
		private bool _eofReached;
		protected string _tag;
		private DateTime _lastProgressPublished;
		private TimeSpan _checkpointAfter;
		private DateTime _lastCheckpointTime = DateTime.MinValue;

		protected ReaderSubscriptionBase(
			IPublisher publisher,
			Guid subscriptionId,
			CheckpointTag @from,
			IReaderStrategy readerStrategy,
			ITimeProvider timeProvider,
			long? checkpointUnhandledBytesThreshold,
			int? checkpointProcessedEventsThreshold,
			int checkpointAfterMs,
			bool stopOnEof,
			int? stopAfterNEvents) {
			if (publisher == null) throw new ArgumentNullException("publisher");
			if (readerStrategy == null) throw new ArgumentNullException("readerStrategy");
			if (timeProvider == null) throw new ArgumentNullException("timeProvider");
			if (checkpointProcessedEventsThreshold > 0 && stopAfterNEvents > 0)
				throw new ArgumentException("checkpointProcessedEventsThreshold > 0 && stopAfterNEvents > 0");

			_publisher = publisher;
			_readerStrategy = readerStrategy;
			_timeProvider = timeProvider;
			_checkpointUnhandledBytesThreshold = checkpointUnhandledBytesThreshold;
			_checkpointProcessedEventsThreshold = checkpointProcessedEventsThreshold;
			_checkpointAfter = TimeSpan.FromMilliseconds(checkpointAfterMs);
			_stopOnEof = stopOnEof;
			_stopAfterNEvents = stopAfterNEvents;
			_subscriptionId = subscriptionId;
			_lastPassedOrCheckpointedEventPosition = null;

			_eventFilter = readerStrategy.EventFilter;

			_positionTagger = readerStrategy.PositionTagger;
			_positionTracker = new PositionTracker(_positionTagger);
			_positionTracker.UpdateByCheckpointTagInitial(@from);
		}

		public string Tag {
			get { return _tag; }
		}

		public Guid SubscriptionId {
			get { return _subscriptionId; }
		}

		protected void ProcessOne(ReaderSubscriptionMessage.CommittedEventDistributed message) {
			if (_eofReached)
				return; // eof may be set by reach N events

			// NOTE: we may receive here messages from heading event distribution point 
			// and they may not pass out source filter.  Discard them first
			var roundedProgress = (float)Math.Round(message.Progress, 1);
			bool progressChanged = _progress != roundedProgress;

			if (
				!_eventFilter.PassesSource(
					message.Data.ResolvedLinkTo, message.Data.PositionStreamId, message.Data.EventType)) {
				if (progressChanged)
					PublishProgress(roundedProgress);
				return;
			}

			// NOTE: after joining heading distribution point it delivers all cached events to the subscription
			// some of this events we may have already received. The delivered events may have different order 
			// (in case of partially ordered cases multi-stream reader etc). We discard all the messages that are not 
			// after the last available checkpoint tag

			//NOTE: older events can appear here when replaying events from the heading event reader
			//      or when event-by-type-index reader reads TF and both event and resolved-event appear as output

			if (!_positionTagger.IsMessageAfterCheckpointTag(_positionTracker.LastTag, message)) {
/*
                _logger.Trace(
                    "Skipping replayed event {positionSequenceNumber}@{positionStreamId} at position {position}. the last processed event checkpoint tag is: {lastTag}",
                    message.PositionSequenceNumber, message.PositionStreamId, message.Position, _positionTracker.LastTag);
*/
				return;
			}

			var eventCheckpointTag = _positionTagger.MakeCheckpointTag(_positionTracker.LastTag, message);
			_positionTracker.UpdateByCheckpointTagForward(eventCheckpointTag);
			var now = _timeProvider.UtcNow;
			var timeDifference = now - _lastCheckpointTime;
			if (_eventFilter.Passes(
				message.Data.ResolvedLinkTo, message.Data.PositionStreamId, message.Data.EventType,
				message.Data.IsStreamDeletedEvent)) {
				_lastPassedOrCheckpointedEventPosition = message.Data.Position.PreparePosition;
				var convertedMessage =
					EventReaderSubscriptionMessage.CommittedEventReceived.FromCommittedEventDistributed(
						message, eventCheckpointTag, _eventFilter.GetCategory(message.Data.PositionStreamId),
						_subscriptionId, _subscriptionMessageSequenceNumber++);
				_publisher.Publish(convertedMessage);
				_eventsSinceLastCheckpointSuggestedOrStart++;
				if (_checkpointProcessedEventsThreshold > 0
				    && timeDifference > _checkpointAfter
				    && _eventsSinceLastCheckpointSuggestedOrStart >= _checkpointProcessedEventsThreshold)
					SuggestCheckpoint(message);
				if (_stopAfterNEvents > 0 && _eventsSinceLastCheckpointSuggestedOrStart >= _stopAfterNEvents)
					NEventsReached();
			} else {
				if (_checkpointUnhandledBytesThreshold > 0
				    && timeDifference > _checkpointAfter
				    && (_lastPassedOrCheckpointedEventPosition != null
				        && message.Data.Position.PreparePosition - _lastPassedOrCheckpointedEventPosition.Value
				        > _checkpointUnhandledBytesThreshold))
					SuggestCheckpoint(message);
				else if (progressChanged)
					PublishProgress(roundedProgress);
			}

			// initialize checkpointing based on first message 
			if (_lastPassedOrCheckpointedEventPosition == null)
				_lastPassedOrCheckpointedEventPosition = message.Data.Position.PreparePosition;
		}

		private void NEventsReached() {
			ProcessEofAndEmitEof();
		}

		private void PublishProgress(float roundedProgress) {
			var now = _timeProvider.UtcNow;
			if (now - _lastProgressPublished > TimeSpan.FromMilliseconds(500)) {
				_lastProgressPublished = now;
				_progress = roundedProgress;
				_publisher.Publish(
					new EventReaderSubscriptionMessage.ProgressChanged(
						_subscriptionId,
						_positionTracker.LastTag,
						_progress,
						_subscriptionMessageSequenceNumber++));
			}
		}

		protected void PublishPartitionDeleted(string partition, CheckpointTag deletePosition) {
			_publisher.Publish(
				new EventReaderSubscriptionMessage.PartitionDeleted(
					_subscriptionId, deletePosition, partition, _subscriptionMessageSequenceNumber++));
		}

		private void PublishStartingAt(long startingLastCommitPosition) {
			_publisher.Publish(
				new EventReaderSubscriptionMessage.SubscriptionStarted(
					_subscriptionId, _positionTracker.LastTag, startingLastCommitPosition,
					_subscriptionMessageSequenceNumber++));
		}

		private void SuggestCheckpoint(ReaderSubscriptionMessage.CommittedEventDistributed message) {
			_lastPassedOrCheckpointedEventPosition = message.Data.Position.PreparePosition;
			_publisher.Publish(
				new EventReaderSubscriptionMessage.CheckpointSuggested(
					_subscriptionId, _positionTracker.LastTag, message.Progress,
					_subscriptionMessageSequenceNumber++));
			_eventsSinceLastCheckpointSuggestedOrStart = 0;
			_lastCheckpointTime = _timeProvider.UtcNow;
		}

		public IEventReader CreatePausedEventReader(IPublisher publisher, IODispatcher ioDispatcher,
			Guid eventReaderId) {
			if (_eofReached)
				throw new InvalidOperationException("Onetime projection has already reached the eof position");
//            _logger.Trace("Creating an event distribution point at '{lastTag}'", _positionTracker.LastTag);
			return _readerStrategy.CreatePausedEventReader(
				eventReaderId, publisher, ioDispatcher, _positionTracker.LastTag, _stopOnEof, _stopAfterNEvents);
		}

		public void Handle(ReaderSubscriptionMessage.EventReaderEof message) {
			if (_eofReached)
				return; // self eof-reached, but reader is still running

			if (_stopOnEof)
				ProcessEofAndEmitEof();
		}

		private void ProcessEofAndEmitEof() {
			_eofReached = true;
			EofReached();
			_publisher.Publish(
				new EventReaderSubscriptionMessage.EofReached(
					_subscriptionId,
					_positionTracker.LastTag,
					_subscriptionMessageSequenceNumber++));
			// self unsubscribe
			_publisher.Publish(new ReaderSubscriptionManagement.Unsubscribe(_subscriptionId));
		}

		public void Handle(ReaderSubscriptionMessage.EventReaderPartitionEof message) {
			if (_eofReached)
				return; // self eof-reached, but reader is still running

			var eventCheckpointTag = _positionTagger.MakeCheckpointTag(_positionTracker.LastTag, message);

			_publisher.Publish(
				new EventReaderSubscriptionMessage.PartitionEofReached(
					_subscriptionId, eventCheckpointTag, message.Partition,
					_subscriptionMessageSequenceNumber++));
		}

		public void Handle(ReaderSubscriptionMessage.EventReaderPartitionMeasured message) {
			if (_eofReached)
				return; // self eof-reached, but reader is still running

			_publisher.Publish(
				new EventReaderSubscriptionMessage.PartitionMeasured(
					_subscriptionId, message.Partition, message.Size,
					_subscriptionMessageSequenceNumber++));
		}

		public void Handle(ReaderSubscriptionMessage.EventReaderNotAuthorized message) {
			if (_eofReached)
				return; // self eof-reached, but reader is still running

			if (_stopOnEof) {
				_eofReached = true;
			}
			_publisher.Publish(
				new EventReaderSubscriptionMessage.NotAuthorized(
					_subscriptionId, _positionTracker.LastTag, _progress, _subscriptionMessageSequenceNumber++));
		}

		public void Handle(ReaderSubscriptionMessage.EventReaderStarting message) {
			PublishStartingAt(message.LastCommitPosition);
		}

		protected virtual void EofReached() {
		}
	}
}
