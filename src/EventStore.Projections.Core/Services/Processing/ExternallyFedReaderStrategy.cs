using System;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Services.UserManagement;

namespace EventStore.Projections.Core.Services.Processing {
	public class ExternallyFedReaderStrategy : IReaderStrategy {
		private readonly string _tag;
		private readonly ITimeProvider _timeProvider;
		private readonly EventFilter _eventFilter;
		private readonly PositionTagger _positionTagger;

		public ExternallyFedReaderStrategy(
			string tag,
			int phase,
			ITimeProvider timeProvider,
			long limitingCommitPosition) {
			_tag = tag;
			_timeProvider = timeProvider;
			_eventFilter = new BypassingEventFilter();
			_positionTagger = new PreTaggedPositionTagger(
				phase,
				CheckpointTag.FromByStreamPosition(phase, "", -1, null, -1, limitingCommitPosition));
		}

		public bool IsReadingOrderRepeatable {
			get { return true; }
		}

		public EventFilter EventFilter {
			get { return _eventFilter; }
		}

		public PositionTagger PositionTagger {
			get { return _positionTagger; }
		}

		public IReaderSubscription CreateReaderSubscription(
			IPublisher publisher, CheckpointTag fromCheckpointTag, Guid subscriptionId,
			ReaderSubscriptionOptions readerSubscriptionOptions) {
			return new ReaderSubscription(
				_tag,
				publisher,
				subscriptionId,
				fromCheckpointTag,
				this,
				_timeProvider,
				readerSubscriptionOptions.CheckpointUnhandledBytesThreshold,
				readerSubscriptionOptions.CheckpointProcessedEventsThreshold,
				readerSubscriptionOptions.CheckpointAfterMs,
				readerSubscriptionOptions.StopOnEof,
				readerSubscriptionOptions.StopAfterNEvents);
		}

		public IEventReader CreatePausedEventReader(
			Guid eventReaderId, IPublisher publisher, IODispatcher ioDispatcher, CheckpointTag checkpointTag,
			bool stopOnEof, int? stopAfterNEvents) {
			return new ExternallyFedByStreamEventReader(
				publisher, eventReaderId, SystemAccount.Principal, ioDispatcher, checkpointTag.CommitPosition,
				_timeProvider, resolveLinkTos: true);
		}
	}
}
