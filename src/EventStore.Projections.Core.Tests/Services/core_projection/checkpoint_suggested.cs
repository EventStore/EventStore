using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.TransactionLog.Data;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;
using EventStore.Projections.Core.Services;

namespace EventStore.Projections.Core.Tests.Services.core_projection {
	public static class checkpoint_suggested {
		[TestFixture]
		public class when_the_checkpoint_is_suggested : TestFixtureWithCoreProjectionStarted {
			protected override void Given() {
				_checkpointHandledThreshold = 10;
				_checkpointUnhandledBytesThreshold = 41;
				_configureBuilderByQuerySource = source => {
					source.FromAll();
					source.IncludeEvent("non-existing");
				};
				NoStream("$projections-projection-state");
				NoStream("$projections-projection-order");
				AllWritesToSucceed("$projections-projection-order");
				NoStream("$projections-projection-checkpoint");
				NoStream(FakeProjectionStateHandler._emit1StreamId);
				AllWritesSucceed();
			}

			protected override void When() {
				//projection subscribes here
				_bus.Publish(
					new EventReaderSubscriptionMessage.CheckpointSuggested(
						_subscriptionId,
						CheckpointTag.FromEventTypeIndexPositions(0, new TFPos(140, 130),
							new Dictionary<string, long> {{"non-existing", -1}}), 55.5f, 0));
			}

			[Test]
			public void a_projection_checkpoint_event_is_published() {
				// projection checkpoint is written even though no events are passing the projection event filter
				Assert.AreEqual(
					1,
					_writeEventHandler.HandledMessages.Count(v =>
						v.Events.Any(e => e.EventType == ProjectionEventTypes.ProjectionCheckpoint)));
			}
		}

		[TestFixture]
		public class when_the_second_checkpoint_is_suggested : TestFixtureWithCoreProjectionStarted {
			protected override void Given() {
				_checkpointHandledThreshold = 10;
				_checkpointUnhandledBytesThreshold = 41;
				_configureBuilderByQuerySource = source => {
					source.FromAll();
					source.IncludeEvent("non-existing");
				};
				NoStream("$$$projections-projection-order");
				NoStream("$projections-projection-order");
				AllWritesToSucceed("$$$projections-projection-order");
				AllWritesToSucceed("$projections-projection-order");

				NoStream("$$$projections-projection-checkpoint");
				NoStream("$projections-projection-checkpoint");
				AllWritesToSucceed("$$$projections-projection-checkpoint");

				NoStream(FakeProjectionStateHandler._emit1StreamId);
				AllWritesQueueUp();
			}

			protected override void When() {
				//projection subscribes here
				_bus.Publish(
					new EventReaderSubscriptionMessage.CheckpointSuggested(
						_subscriptionId,
						CheckpointTag.FromEventTypeIndexPositions(0, new TFPos(140, 130),
							new Dictionary<string, long> {{"non-existing", -1}}), 55.5f, 0));
				_bus.Publish(
					new EventReaderSubscriptionMessage.CheckpointSuggested(
						_subscriptionId,
						CheckpointTag.FromEventTypeIndexPositions(0, new TFPos(160, 150),
							new Dictionary<string, long> {{"non-existing", -1}}), 55.6f, 1));
			}

			[Test]
			public void a_projection_checkpoint_event_is_published() {
				// projection checkpoint is written even though no events are passing the projection event filter
				Assert.AreEqual(
					1,
					_writeEventHandler.HandledMessages.Count(v =>
						v.Events.Any(e => e.EventType == ProjectionEventTypes.ProjectionCheckpoint)));
			}
		}

		[TestFixture]
		public class when_the_second_checkpoint_is_suggested_and_write_succeeds : TestFixtureWithCoreProjectionStarted {
			protected override void Given() {
				_checkpointHandledThreshold = 10;
				_checkpointUnhandledBytesThreshold = 41;
				_configureBuilderByQuerySource = source => {
					source.FromAll();
					source.IncludeEvent("non-existing");
				};
				NoStream("$projections-projection-state");
				NoStream("$projections-projection-order");
				AllWritesToSucceed("$projections-projection-order");
				NoStream("$projections-projection-checkpoint");
				NoStream(FakeProjectionStateHandler._emit1StreamId);
				AllWritesSucceed();
			}

			protected override void When() {
				//projection subscribes here
				_bus.Publish(
					new EventReaderSubscriptionMessage.CheckpointSuggested(
						_subscriptionId,
						CheckpointTag.FromEventTypeIndexPositions(0, new TFPos(140, 130),
							new Dictionary<string, long> {{"non-existing", -1}}), 55.5f, 0));
				_bus.Publish(
					new EventReaderSubscriptionMessage.CheckpointSuggested(
						_subscriptionId,
						CheckpointTag.FromEventTypeIndexPositions(0, new TFPos(160, 150),
							new Dictionary<string, long> {{"non-existing", -1}}), 55.6f, 1));
			}

			[Test]
			public void a_projection_checkpoint_event_is_published() {
				// projection checkpoint is written even though no events are passing the projection event filter
				Assert.AreEqual(
					2,
					_writeEventHandler.HandledMessages.Count(v =>
						v.Events.Any(e => e.EventType == ProjectionEventTypes.ProjectionCheckpoint)));
			}
		}
	}
}
