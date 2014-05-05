using System;
using System.Linq;
using EventStore.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.emitted_stream
{
    [TestFixture]
    public class when_checkpoint_requested_with_pending_writes : TestFixtureWithExistingEvents
    {
        private EmittedStream _stream;
        private TestCheckpointManagerMessageHandler _readyHandler;

        protected override void Given()
        {
            base.Given();
            AllWritesSucceed();
            NoOtherStreams();
        }

        [SetUp]
        public void setup()
        {
            _readyHandler = new TestCheckpointManagerMessageHandler();
            ;
            _stream = new EmittedStream(
                "test", new EmittedStream.WriterConfiguration(new EmittedStream.WriterConfiguration.StreamMetadata(), null, 50), new ProjectionVersion(1, 0, 0),
                new TransactionFilePositionTagger(0), CheckpointTag.FromPosition(0, 0, -1), _ioDispatcher, _readyHandler);
            _stream.Start();
            _stream.EmitEvents(
                new[]
                {
                    new EmittedDataEvent(
                        "test", Guid.NewGuid(), "type", true, "data", null, CheckpointTag.FromPosition(0, 100, 50), null)
                });
            _stream.Checkpoint();
        }

        [Test]
        public void does_not_publish_ready_for_checkpoint_immediately()
        {
            Assert.AreEqual(
                0, _consumer.HandledMessages.OfType<CoreProjectionProcessingMessage.ReadyForCheckpoint>().Count());
        }

        [Test]
        public void publishes_ready_for_checkpoint_on_handling_last_write_events_completed()
        {
            var msg = _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().First();
            _bus.Publish(new ClientMessage.WriteEventsCompleted(msg.CorrelationId, 0, 0, -1, -1));
            Assert.AreEqual(
                1, _readyHandler.HandledMessages.OfType<CoreProjectionProcessingMessage.ReadyForCheckpoint>().Count());
        }
    }
}
