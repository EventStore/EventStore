using System;
using EventStore.Core.Tests.Bus.Helpers;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection.projection_checkpoint
{
    [TestFixture]
    public class the_started_checkpoint_with_some_events_emitted : TestFixtureWithExistingEvents
    {
        private ProjectionCheckpoint _checkpoint;
        private TestCheckpointManagerMessageHandler _readyHandler;

        [SetUp]
        public void setup()
        {
            _readyHandler = new TestCheckpointManagerMessageHandler();;
            _checkpoint = new ProjectionCheckpoint(
                _ioDispatcher, new ProjectionVersion(1, 0, 0), null, _readyHandler,
                CheckpointTag.FromPosition(0, 100, 50), new TransactionFilePositionTagger(0), 250);
            _checkpoint.Start();
            _checkpoint.ValidateOrderAndEmitEvents(
                new[]
                    {
                        new EmittedEventEnvelope(new EmittedDataEvent(
                    "stream2", Guid.NewGuid(), "type", true, "data2", null, CheckpointTag.FromPosition(0, 120, 110), null)),
                        new EmittedEventEnvelope(new EmittedDataEvent(
                    "stream2", Guid.NewGuid(), "type", true, "data4", null, CheckpointTag.FromPosition(0, 120, 110), null)),
                    });
            _checkpoint.ValidateOrderAndEmitEvents(
                new[]
                    {
                        new EmittedEventEnvelope(new EmittedDataEvent(
                    "stream1", Guid.NewGuid(), "type", true, "data", null, CheckpointTag.FromPosition(0, 140, 130), null))
                    });
            _checkpoint.ValidateOrderAndEmitEvents(
                new[]
                    {
                        new EmittedEventEnvelope(new EmittedDataEvent(
                    "stream1", Guid.NewGuid(), "type", true, "data", null, CheckpointTag.FromPosition(0, 160, 150), null))
                    });
        }

        [Test]
        public void requesting_checkpoints_with_position_before_the_last_known_throws_invalid_operation_exception()
        {
            Assert.Throws<InvalidOperationException>(()=> { _checkpoint.Prepare(CheckpointTag.FromPosition(0, 140, 130)); });
        }
    }
}
