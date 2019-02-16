using System;
using System.Linq;
using EventStore.Core.Messages;
using EventStore.Core.Tests.Helpers;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;
using TestFixtureWithExistingEvents =
	EventStore.Projections.Core.Tests.Services.core_projection.TestFixtureWithExistingEvents;

namespace EventStore.Projections.Core.Tests.Services.emitted_stream {
	[TestFixture]
	public class when_checkpoint_requested_with_all_writes_already_completed : TestFixtureWithExistingEvents {
		private EmittedStream _stream;
		private TestCheckpointManagerMessageHandler _readyHandler;

		protected override void Given() {
			base.Given();
			AllWritesSucceed();
			NoOtherStreams();
		}

		[SetUp]
		public void Setup() {
			_readyHandler = new TestCheckpointManagerMessageHandler();
			_stream = new EmittedStream(
				"test",
				new EmittedStream.WriterConfiguration(new EmittedStreamsWriter(_ioDispatcher),
					new EmittedStream.WriterConfiguration.StreamMetadata(), null, 50), new ProjectionVersion(1, 0, 0),
				new TransactionFilePositionTagger(0), CheckpointTag.FromPosition(0, 0, -1), _bus, _ioDispatcher,
				_readyHandler);
			_stream.Start();
			_stream.EmitEvents(
				new[] {
					new EmittedDataEvent(
						"test", Guid.NewGuid(), "type", true, "data", null, CheckpointTag.FromPosition(0, 10, 5), null)
				});
			var msg = _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().First();
			_bus.Publish(new ClientMessage.WriteEventsCompleted(msg.CorrelationId, 0, 0, -1, -1));
			_stream.Checkpoint();
		}

		[Test]
		public void publishes_ready_for_checkpoint() {
			Assert.IsTrue(
				_readyHandler.HandledMessages.ContainsSingle<CoreProjectionProcessingMessage.ReadyForCheckpoint>());
		}
	}
}
