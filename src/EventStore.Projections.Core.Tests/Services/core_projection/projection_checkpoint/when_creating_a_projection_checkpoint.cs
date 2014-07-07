using System;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Fakes;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection.projection_checkpoint
{
    [TestFixture]
    public class when_creating_a_projection_checkpoint
    {
        private FakePublisher _fakePublisher;
        private TestCheckpointManagerMessageHandler _readyHandler;
        private IODispatcher _ioDispatcher;


        [SetUp]
        public void setup()
        {
            _readyHandler = new TestCheckpointManagerMessageHandler();
            _fakePublisher = new FakePublisher();
            _ioDispatcher = new IODispatcher(_fakePublisher, new PublishEnvelope(_fakePublisher));
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void null_io_dispatcher_throws_argument_null_exception()
        {
            new ProjectionCheckpoint(
                null, new ProjectionVersion(1, 0, 0), null, _readyHandler,
                CheckpointTag.FromPosition(0, 100, 50), new TransactionFilePositionTagger(0), 250);
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void null_ready_handler_throws_argument_null_exception()
        {
            new ProjectionCheckpoint(
                _ioDispatcher, new ProjectionVersion(1, 0, 0), null, null,
                CheckpointTag.FromPosition(0, 100, 50), new TransactionFilePositionTagger(0), 250);
        }

        [Test, ExpectedException(typeof (ArgumentException))]
        public void commit_position_less_than_or_equal_to_prepare_position_throws_argument_exception()
        {
            new ProjectionCheckpoint(
                _ioDispatcher, new ProjectionVersion(1, 0, 0), null, _readyHandler,
                CheckpointTag.FromPosition(0, 100, 101), new TransactionFilePositionTagger(0), 250);
        }

        [Test]
        public void it_can_be_created()
        {
            new ProjectionCheckpoint(
                _ioDispatcher, new ProjectionVersion(1, 0, 0), null, _readyHandler,
                CheckpointTag.FromPosition(0, 100, 50), new TransactionFilePositionTagger(0), 250);
        }
    }
}
