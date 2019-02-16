using System;
using System.Security.Principal;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Core.Services.TimerService;

namespace EventStore.Projections.Core.Services.Processing {
	public class ParallelQueryAllStreamsMasterReaderStrategy : IReaderStrategy {
		private readonly string _tag;
		private readonly IPrincipal _runAs;
		private readonly ITimeProvider _timeProvider;
		private readonly EventFilter _eventFilter;
		private readonly PositionTagger _positionTagger;

		public ParallelQueryAllStreamsMasterReaderStrategy(
			string tag,
			int phase,
			IPrincipal runAs,
			ITimeProvider timeProvider) {
			_tag = tag;
			_runAs = runAs;
			_timeProvider = timeProvider;
			_eventFilter = new StreamEventFilter("$streams", true, null);
			_positionTagger = new CatalogStreamPositionTagger(phase, "$streams");
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
			return new AllStreamsCatalogEventReader(
				ioDispatcher, publisher, eventReaderId, _runAs, checkpointTag.CatalogPosition + 1, _timeProvider,
				stopOnEof: stopOnEof);
		}
	}
}
