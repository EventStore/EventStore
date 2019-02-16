using System;
using System.Linq;
using EventStore.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;
using EventStore.Projections.Core.Common;

namespace EventStore.Projections.Core.Tests.Services.core_projection.projection_checkpoint {
	[TestFixture]
	public class when_starting_the_projection_checkpoint_with_some_events_already_emitted :
		TestFixtureWithExistingEvents {
		private ProjectionCheckpoint _checkpoint;
		private TestCheckpointManagerMessageHandler _readyHandler;

		protected override void Given() {
			NoStream("stream1");
			NoStream("stream2");
			NoStream("stream3");
		}

		[SetUp]
		public void setup() {
			_readyHandler = new TestCheckpointManagerMessageHandler();
			_checkpoint = new ProjectionCheckpoint(
				_bus, _ioDispatcher, new ProjectionVersion(1, 0, 0), null, _readyHandler,
				CheckpointTag.FromPosition(0, 100, 50), new TransactionFilePositionTagger(0), 250,
				AllowedWritesInFlight.Unbounded);
			_checkpoint.ValidateOrderAndEmitEvents(
				new[] {
					new EmittedEventEnvelope(
						new EmittedDataEvent(
							"stream2", Guid.NewGuid(), "type", true, "data2", null,
							CheckpointTag.FromPosition(0, 120, 110), null)),
					new EmittedEventEnvelope(
						new EmittedDataEvent(
							"stream3", Guid.NewGuid(), "type", true, "data3", null,
							CheckpointTag.FromPosition(0, 120, 110), null)),
					new EmittedEventEnvelope(
						new EmittedDataEvent(
							"stream2", Guid.NewGuid(), "type", true, "data4", null,
							CheckpointTag.FromPosition(0, 120, 110), null)),
				});
			_checkpoint.ValidateOrderAndEmitEvents(
				new[] {
					new EmittedEventEnvelope(
						new EmittedDataEvent(
							"stream1", Guid.NewGuid(), "type", true, "data", null,
							CheckpointTag.FromPosition(0, 140, 130), null))
				});
			_checkpoint.Start();
		}

		[Test]
		public void should_publish_write_events() {
			Assert.AreEqual(3, _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().Count());
		}
	}
}
