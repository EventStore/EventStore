using System;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.ParallelQueryProcessingMessages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;
using ResolvedEvent = EventStore.Projections.Core.Services.Processing.ResolvedEvent;

namespace EventStore.Projections.Core.Tests.Services.core_projection.slave_projection {
	public abstract class specification_with_slave_core_projection : TestFixtureWithCoreProjectionStarted {
		protected Guid _eventId;

		protected override bool GivenIsSlaveProjection() {
			return true;
		}

		protected override bool GivenCheckpointsEnabled() {
			return false;
		}

		protected override bool GivenEmitEventEnabled() {
			return false;
		}

		protected override bool GivenStopOnEof() {
			return true;
		}

		protected override int GivenPendingEventsThreshold() {
			return 0;
		}

		protected override ProjectionProcessingStrategy GivenProjectionProcessingStrategy() {
			return new SlaveQueryProcessingStrategy(
				_projectionName,
				_version,
				_stateHandler,
				_projectionConfig,
				_stateHandler.GetSourceDefinition(),
				null,
				_workerId,
				GetInputQueue(),
				_projectionCorrelationId,
				_subscriptionDispatcher);
		}

		protected override void Given() {
			_eventId = Guid.NewGuid();
			_checkpointHandledThreshold = 0;
			_checkpointUnhandledBytesThreshold = 0;

			_configureBuilderByQuerySource = source => {
				source.FromCatalogStream("catalog");
				source.AllEvents();
				source.SetOutputState();
				source.SetByStream();
			};
			TicksAreHandledImmediately();
			AllWritesSucceed();
			NoOtherStreams();
		}

		protected override FakeProjectionStateHandler GivenProjectionStateHandler() {
			return new FakeProjectionStateHandler(
				configureBuilder: _configureBuilderByQuerySource, failOnGetPartition: false);
		}
	}

	[TestFixture]
	class when_processes_one_partition : specification_with_slave_core_projection {
		protected override void When() {
			_bus.Publish(
				EventReaderSubscriptionMessage.CommittedEventReceived.Sample(
					new ResolvedEvent(
						"account-01", -1, "account-01", 0, false, new TFPos(120, 110), Guid.NewGuid(),
						"handle_this_type", false, "data", "metadata"),
					CheckpointTag.FromByStreamPosition(0, "", 0, "account-01", 0, 500), _subscriptionId, 0));
			_bus.Publish(
				EventReaderSubscriptionMessage.CommittedEventReceived.Sample(
					new ResolvedEvent(
						"account-01", -1, "account-01", 1, false, new TFPos(220, 210), _eventId, "handle_this_type",
						false, "data", "metadata"), CheckpointTag.FromByStreamPosition(0, "", 0, "account-01", 1, 500),
					_subscriptionId, 1));
			_bus.Publish(
				new EventReaderSubscriptionMessage.PartitionEofReached(
					_subscriptionId, CheckpointTag.FromByStreamPosition(0, "", 0, "account-01", long.MaxValue, 500),
					"account-01", 2));
		}

		[Test]
		public void publishes_partition_processing_result_message() {
			var results = HandledMessages.OfType<PartitionProcessingResult>().ToArray();
			Assert.AreEqual(1, results.Length);
		}
	}

	[TestFixture]
	class when_processes_multiple_partitions : specification_with_slave_core_projection {
		protected override void When() {
			_bus.Publish(
				EventReaderSubscriptionMessage.CommittedEventReceived.Sample(
					new ResolvedEvent(
						"account-01", -1, "account-01", 0, false, new TFPos(120, 110), Guid.NewGuid(),
						"handle_this_type", false, "data", "metadata"),
					CheckpointTag.FromByStreamPosition(0, "", 0, "account-01", 0, 500), _subscriptionId, 0));
			_bus.Publish(
				EventReaderSubscriptionMessage.CommittedEventReceived.Sample(
					new ResolvedEvent(
						"account-01", -1, "account-01", 1, false, new TFPos(220, 210), _eventId, "handle_this_type",
						false, "data", "metadata"), CheckpointTag.FromByStreamPosition(0, "", 0, "account-01", 1, 500),
					_subscriptionId, 1));
			_bus.Publish(
				new EventReaderSubscriptionMessage.PartitionEofReached(
					_subscriptionId, CheckpointTag.FromByStreamPosition(0, "", 0, "account-01", long.MaxValue, 500),
					"account-01", 2));
			_bus.Publish(
				EventReaderSubscriptionMessage.CommittedEventReceived.Sample(
					new ResolvedEvent(
						"account-02", -1, "account-02", 0, false, new TFPos(220, 210), _eventId, "handle_this_type",
						false, "data", "metadata"), CheckpointTag.FromByStreamPosition(0, "", 1, "account-02", 0, 500),
					_subscriptionId, 3));
			_bus.Publish(
				new EventReaderSubscriptionMessage.PartitionEofReached(
					_subscriptionId, CheckpointTag.FromByStreamPosition(0, "", 1, "account-02", long.MaxValue, 500),
					"account-01", 4));
		}

		[Test]
		public void publishes_all_partition_processing_result_messages() {
			var results = HandledMessages.OfType<PartitionProcessingResult>().ToArray();
			Assert.AreEqual(2, results.Length);
		}
	}
}
