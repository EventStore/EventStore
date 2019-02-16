using System;
using System.Linq;
using EventStore.Core.Messages;
using EventStore.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;
using System.Collections;
using EventStore.Projections.Core.Common;

namespace EventStore.Projections.Core.Tests.Services.core_projection.projection_checkpoint {
	public class NumberOfMessagesInFlightSource : IEnumerable {
		public IEnumerator GetEnumerator() {
			return Enumerable.Range(1, 3).Select(x => x)
				.GetEnumerator();
		}
	}

	[TestFixture, TestFixtureSource(typeof(NumberOfMessagesInFlightSource))]
	public class when_emitting_events_with_maximum_allowed_writes_in_flight_set : TestFixtureWithExistingEvents {
		private ProjectionCheckpoint _checkpoint;
		private TestCheckpointManagerMessageHandler _readyHandler;

		private int _maximumNumberOfAllowedWritesInFlight;

		public when_emitting_events_with_maximum_allowed_writes_in_flight_set(
			int maximumNumberOfAllowedWritesInFlight) {
			_maximumNumberOfAllowedWritesInFlight = maximumNumberOfAllowedWritesInFlight;
		}

		protected override void Given() {
			AllWritesQueueUp();
			AllWritesToSucceed("$$stream1");
			AllWritesToSucceed("$$stream2");
			AllWritesToSucceed("$$stream3");
			NoOtherStreams();
		}

		[SetUp]
		public void setup() {
			_readyHandler = new TestCheckpointManagerMessageHandler();
			_checkpoint = new ProjectionCheckpoint(
				_bus, _ioDispatcher, new ProjectionVersion(1, 0, 0), null, _readyHandler,
				CheckpointTag.FromPosition(0, 100, 50), new TransactionFilePositionTagger(0), 250,
				_maximumNumberOfAllowedWritesInFlight);
			_checkpoint.Start();
			_checkpoint.ValidateOrderAndEmitEvents(
				new[] {
					new EmittedEventEnvelope(
						new EmittedDataEvent(
							"stream1", Guid.NewGuid(), "type1", true, "data1", null,
							CheckpointTag.FromPosition(0, 120, 110), null)),
					new EmittedEventEnvelope(
						new EmittedDataEvent(
							"stream2", Guid.NewGuid(), "type2", true, "data2", null,
							CheckpointTag.FromPosition(0, 120, 110), null)),
					new EmittedEventEnvelope(
						new EmittedDataEvent(
							"stream3", Guid.NewGuid(), "type3", true, "data3", null,
							CheckpointTag.FromPosition(0, 120, 110), null)),
				});
		}

		[Test]
		public void should_have_the_same_number_writes_in_flight_as_configured() {
			var writeEvents =
				_consumer.HandledMessages.OfType<ClientMessage.WriteEvents>()
					.ExceptOfEventType(SystemEventTypes.StreamMetadata);
			Assert.AreEqual(_maximumNumberOfAllowedWritesInFlight, writeEvents.Count());
		}
	}

	public class
		when_emitting_events_with_maximum_allowed_writes_in_flight_set_to_unlimited : TestFixtureWithExistingEvents {
		private ProjectionCheckpoint _checkpoint;
		private TestCheckpointManagerMessageHandler _readyHandler;

		protected override void Given() {
			AllWritesQueueUp();
			AllWritesToSucceed("$$stream1");
			AllWritesToSucceed("$$stream2");
			AllWritesToSucceed("$$stream3");
			NoOtherStreams();
		}

		[SetUp]
		public void setup() {
			_readyHandler = new TestCheckpointManagerMessageHandler();
			_checkpoint = new ProjectionCheckpoint(
				_bus, _ioDispatcher, new ProjectionVersion(1, 0, 0), null, _readyHandler,
				CheckpointTag.FromPosition(0, 100, 50), new TransactionFilePositionTagger(0), 250,
				AllowedWritesInFlight.Unbounded);
			_checkpoint.Start();
			_checkpoint.ValidateOrderAndEmitEvents(
				new[] {
					new EmittedEventEnvelope(
						new EmittedDataEvent(
							"stream1", Guid.NewGuid(), "type1", true, "data1", null,
							CheckpointTag.FromPosition(0, 120, 110), null)),
					new EmittedEventEnvelope(
						new EmittedDataEvent(
							"stream2", Guid.NewGuid(), "type2", true, "data2", null,
							CheckpointTag.FromPosition(0, 120, 110), null)),
					new EmittedEventEnvelope(
						new EmittedDataEvent(
							"stream3", Guid.NewGuid(), "type3", true, "data3", null,
							CheckpointTag.FromPosition(0, 120, 110), null)),
				});
		}

		[Test]
		public void should_have_as_many_writes_in_flight_as_requested() {
			var writeEvents =
				_consumer.HandledMessages.OfType<ClientMessage.WriteEvents>()
					.ExceptOfEventType(SystemEventTypes.StreamMetadata);
			Assert.AreEqual(3, writeEvents.Count());
		}
	}
}
