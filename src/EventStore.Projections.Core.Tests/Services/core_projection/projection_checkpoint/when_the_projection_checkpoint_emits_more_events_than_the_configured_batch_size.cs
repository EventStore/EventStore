using System;
using System.Linq;
using EventStore.Core.Messages;
using EventStore.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection.projection_checkpoint
{
    [TestFixture]
    public class when_the_projection_checkpoint_emits_more_events_than_the_configured_batch_size : TestFixtureWithExistingEvents
    {
        private ProjectionCheckpoint _checkpoint;
        private TestCheckpointManagerMessageHandler _readyHandler;

        protected override void Given()
        {
            NoOtherStreams();
            AllWritesQueueUp();
        }

        [SetUp]
        public void setup()
        {
            _readyHandler = new TestCheckpointManagerMessageHandler();
            _checkpoint = new ProjectionCheckpoint(
                _ioDispatcher, new ProjectionVersion(1, 0, 0), null, _readyHandler,
                CheckpointTag.FromPosition(0, 0, -1), new TransactionFilePositionTagger(0), 5);
            _checkpoint.Start();
            _checkpoint.ValidateOrderAndEmitEvents(
                new[]
                {
                    new EmittedEventEnvelope(
                        new EmittedDataEvent(
                            "stream1", Guid.NewGuid(), "type", true, "data2", null, CheckpointTag.FromPosition(0, 10, 10), null)),
                    new EmittedEventEnvelope(
                        new EmittedDataEvent(
                            "stream1", Guid.NewGuid(), "type", true, "data4", null, CheckpointTag.FromPosition(0, 10, 10), null)),
                    new EmittedEventEnvelope(
                        new EmittedDataEvent(
                            "stream1", Guid.NewGuid(), "type", true, "data4", null, CheckpointTag.FromPosition(0, 10, 10), null)),
                    new EmittedEventEnvelope(
                        new EmittedDataEvent(
                            "stream1", Guid.NewGuid(), "type", true, "data4", null, CheckpointTag.FromPosition(0, 10, 10), null)),
                    new EmittedEventEnvelope(
                        new EmittedDataEvent(
                            "stream1", Guid.NewGuid(), "type", true, "data4", null, CheckpointTag.FromPosition(0, 10, 10), null)),
                    new EmittedEventEnvelope(
                        new EmittedDataEvent(
                            "stream1", Guid.NewGuid(), "type", true, "data4", null, CheckpointTag.FromPosition(0, 10, 10), null)),
                    new EmittedEventEnvelope(
                        new EmittedDataEvent(
                            "stream1", Guid.NewGuid(), "type", true, "data4", null, CheckpointTag.FromPosition(0, 10, 10), null)),
                    new EmittedEventEnvelope(
                        new EmittedDataEvent(
                            "stream1", Guid.NewGuid(), "type", true, "data4", null, CheckpointTag.FromPosition(0, 10, 10), null)),
                });
            OneWriteCompletes(); //metadata
            OneWriteCompletes(); //first batch of 5
        }

        [Test]
        public void it_should_write_all_the_events()
        {
            var writeEvents =
                _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().Where(x => x.EventStreamId == "stream1");
            Assert.AreEqual(2, writeEvents.Count());
        }
    }
}
