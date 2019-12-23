using System;
using EventStore.Core.Bus;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Tests.Bus.Helpers;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projection_subscription {
	public abstract class TestFixtureWithProjectionSubscription {
		protected Guid _projectionCorrelationId;
		protected TestHandler<EventReaderSubscriptionMessage.CommittedEventReceived> _eventHandler;
		protected TestHandler<EventReaderSubscriptionMessage.CheckpointSuggested> _checkpointHandler;
		protected TestHandler<EventReaderSubscriptionMessage.ProgressChanged> _progressHandler;
		protected TestHandler<EventReaderSubscriptionMessage.SubscriptionStarted> _subscriptionStartedHandler;
		protected TestHandler<EventReaderSubscriptionMessage.NotAuthorized> _notAuthorizedHandler;
		protected TestHandler<EventReaderSubscriptionMessage.EofReached> _eofHandler;
		protected TestHandler<EventReaderSubscriptionMessage.PartitionEofReached> _partitionEofHandler;
		protected TestHandler<EventReaderSubscriptionMessage.PartitionDeleted> _partitionDeletedHandler;
		protected IReaderSubscription _subscription;
		protected ITimeProvider _timeProvider;
		protected IEventReader ForkedReader;
		protected InMemoryBus _bus;
		protected Action<SourceDefinitionBuilder> _source = null;
		protected int _checkpointUnhandledBytesThreshold;
		protected int _checkpointProcessedEventsThreshold;
		protected int _checkpointAfterMs;
		protected IReaderStrategy _readerStrategy;

		[SetUp]
		public void setup() {
			_checkpointUnhandledBytesThreshold = 1000;
			_checkpointProcessedEventsThreshold = 2000;
			_checkpointAfterMs = 10000;
			_timeProvider = new RealTimeProvider();
			Given();
			_bus = new InMemoryBus("bus");
			_projectionCorrelationId = Guid.NewGuid();
			_eventHandler = new TestHandler<EventReaderSubscriptionMessage.CommittedEventReceived>();
			_checkpointHandler = new TestHandler<EventReaderSubscriptionMessage.CheckpointSuggested>();
			_progressHandler = new TestHandler<EventReaderSubscriptionMessage.ProgressChanged>();
			_subscriptionStartedHandler = new TestHandler<EventReaderSubscriptionMessage.SubscriptionStarted>();
			_notAuthorizedHandler = new TestHandler<EventReaderSubscriptionMessage.NotAuthorized>();
			_eofHandler = new TestHandler<EventReaderSubscriptionMessage.EofReached>();
			_partitionEofHandler = new TestHandler<EventReaderSubscriptionMessage.PartitionEofReached>();
			_partitionDeletedHandler = new TestHandler<EventReaderSubscriptionMessage.PartitionDeleted>();

			_bus.Subscribe(_eventHandler);
			_bus.Subscribe(_checkpointHandler);
			_bus.Subscribe(_progressHandler);
			_bus.Subscribe(_eofHandler);
			_bus.Subscribe(_partitionEofHandler);
			_readerStrategy = CreateCheckpointStrategy();
			_subscription = CreateProjectionSubscription();


			When();
		}

		protected virtual IReaderSubscription CreateProjectionSubscription() {
			return new ReaderSubscription(
				"Test Subscription",
				_bus,
				_projectionCorrelationId,
				_readerStrategy.PositionTagger.MakeZeroCheckpointTag(),
				_readerStrategy,
				_timeProvider,
				_checkpointUnhandledBytesThreshold,
				_checkpointProcessedEventsThreshold,
				_checkpointAfterMs);
		}

		protected virtual void Given() {
		}

		protected abstract void When();

		protected virtual IReaderStrategy CreateCheckpointStrategy() {
			var readerBuilder = new SourceDefinitionBuilder();
			if (_source != null) {
				_source(readerBuilder);
			} else {
				readerBuilder.FromAll();
				readerBuilder.AllEvents();
			}

			var config = ProjectionConfig.GetTest();
			IQuerySources sources = readerBuilder.Build();
			var readerStrategy = Core.Services.Processing.ReaderStrategy.Create(
				"test",
				0,
				sources,
				_timeProvider,
				stopOnEof: false,
				runAs: config.RunAs);
			return readerStrategy;
		}
	}
}
