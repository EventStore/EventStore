using System;
using System.Collections.Generic;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Tests.Bus.Helpers;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.ParallelQueryProcessingMessages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Management;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;
using ResolvedEvent = EventStore.Projections.Core.Services.Processing.ResolvedEvent;

namespace EventStore.Projections.Core.Tests.Services {
	public class TestFixtureWithProjectionCoreService {
		public class TestCoreProjection : ICoreProjection {
			public List<EventReaderSubscriptionMessage.CommittedEventReceived> HandledMessages =
				new List<EventReaderSubscriptionMessage.CommittedEventReceived>();

			public void Handle(CoreProjectionProcessingMessage.CheckpointCompleted message) {
				throw new NotImplementedException();
			}

			public void Handle(CoreProjectionProcessingMessage.CheckpointLoaded message) {
				throw new NotImplementedException();
			}


			public void Handle(CoreProjectionProcessingMessage.RestartRequested message) {
				throw new NotImplementedException();
			}

			public void Handle(CoreProjectionProcessingMessage.Failed message) {
				throw new NotImplementedException();
			}

			public void Handle(CoreProjectionProcessingMessage.PrerecordedEventsLoaded message) {
				throw new NotImplementedException();
			}
		}

		protected TestHandler<Message> _consumer;
		protected InMemoryBus _bus;
		protected ProjectionCoreService _service;
		protected EventReaderCoreService _readerService;

		private
			ReaderSubscriptionDispatcher
			_subscriptionDispatcher;

		private SpooledStreamReadingDispatcher _spoolProcessingResponseDispatcher;
		private ISingletonTimeoutScheduler _timeoutScheduler;
		protected Guid _workerId;

		[SetUp]
		public void Setup() {
			_consumer = new TestHandler<Message>();
			_bus = new InMemoryBus("temp");
			_bus.Subscribe(_consumer);
			ICheckpoint writerCheckpoint = new InMemoryCheckpoint(1000);
			var ioDispatcher = new IODispatcher(_bus, new PublishEnvelope(_bus));
			_readerService = new EventReaderCoreService(_bus, ioDispatcher, 10, writerCheckpoint,
				runHeadingReader: true, faultOutOfOrderProjections: true);
			_subscriptionDispatcher =
				new ReaderSubscriptionDispatcher(_bus);
			_spoolProcessingResponseDispatcher = new SpooledStreamReadingDispatcher(_bus);
			_timeoutScheduler = new TimeoutScheduler();
			_workerId = Guid.NewGuid();
			_service = new ProjectionCoreService(
				_workerId, _bus, _bus, _subscriptionDispatcher, new RealTimeProvider(), ioDispatcher,
				_spoolProcessingResponseDispatcher, _timeoutScheduler);
			_bus.Subscribe(
				_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.CheckpointSuggested>());
			_bus.Subscribe(_subscriptionDispatcher
				.CreateSubscriber<EventReaderSubscriptionMessage.CommittedEventReceived>());
			_bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.EofReached>());
			_bus.Subscribe(
				_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.PartitionEofReached>());
			_bus.Subscribe(_subscriptionDispatcher
				.CreateSubscriber<EventReaderSubscriptionMessage.PartitionMeasured>());
			_bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.PartitionDeleted>());
			_bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.ProgressChanged>());
			_bus.Subscribe(
				_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.SubscriptionStarted>());
			_bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.NotAuthorized>());
			_bus.Subscribe(
				_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.ReaderAssignedReader>());
			_bus.Subscribe(_spoolProcessingResponseDispatcher.CreateSubscriber<PartitionProcessingResult>());
			_readerService.Handle(new Messages.ReaderCoreServiceMessage.StartReader());
			_service.Handle(new ProjectionCoreServiceMessage.StartCore(Guid.NewGuid()));
		}

		protected IReaderStrategy CreateReaderStrategy() {
			var result = new SourceDefinitionBuilder();
			result.FromAll();
			result.AllEvents();
			return ReaderStrategy.Create(
				"test",
				0,
				result.Build(),
				new RealTimeProvider(),
				stopOnEof: false,
				runAs: null);
		}

		protected static ResolvedEvent CreateEvent() {
			return new ResolvedEvent(
				"test", -1, "test", -1, false, new TFPos(10, 5), new TFPos(10, 5), Guid.NewGuid(), "t", false,
				new byte[0], new byte[0], null, null, default(DateTime));
		}
	}
}
