using System;
using EventStore.Core.Helpers;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Fakes;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.emitted_stream
{
    [TestFixture]
    public class when_creating_an_emitted_stream
    {
        private FakePublisher _fakePublisher;
        private IODispatcher _ioDispatcher;


        [SetUp]
        public void setup()
        {
            _fakePublisher = new FakePublisher();
            _ioDispatcher = new IODispatcher(_fakePublisher, new PublishEnvelope(_fakePublisher));
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void null_stream_id_throws_argument_null_exception()
        {
            new EmittedStream(
                null, new EmittedStream.WriterConfiguration(new EmittedStream.WriterConfiguration.StreamMetadata(), null, 50), new ProjectionVersion(1, 0, 0),
                new TransactionFilePositionTagger(0), CheckpointTag.FromPosition(0, 0, -1), _ioDispatcher,
                new TestCheckpointManagerMessageHandler());
        }

        [Test, ExpectedException(typeof(ArgumentNullException))]
        public void null_writer_configuration_throws_argument_null_exception()
        {
            new EmittedStream(
                null, null, new ProjectionVersion(1, 0, 0),
                new TransactionFilePositionTagger(0), CheckpointTag.FromPosition(0, 0, -1), _ioDispatcher,
                new TestCheckpointManagerMessageHandler());
        }

        [Test, ExpectedException(typeof(ArgumentException))]
        public void empty_stream_id_throws_argument_exception()
        {
            new EmittedStream(
                "", new EmittedStream.WriterConfiguration(new EmittedStream.WriterConfiguration.StreamMetadata(), null, 50), new ProjectionVersion(1, 0, 0),
                new TransactionFilePositionTagger(0), CheckpointTag.FromPosition(0, 0, -1), _ioDispatcher,
                new TestCheckpointManagerMessageHandler());
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void null_from_throws_argument_exception()
        {
            new EmittedStream(
                "", new EmittedStream.WriterConfiguration(new EmittedStream.WriterConfiguration.StreamMetadata(), null, 50), new ProjectionVersion(1, 0, 0),
                new TransactionFilePositionTagger(0), null, _ioDispatcher, new TestCheckpointManagerMessageHandler());
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void null_io_dispatcher_throws_argument_null_exception()
        {
            new EmittedStream(
                "test", new EmittedStream.WriterConfiguration(new EmittedStream.WriterConfiguration.StreamMetadata(), null, 50), new ProjectionVersion(1, 0, 0),
                new TransactionFilePositionTagger(0), CheckpointTag.FromPosition(0, 0, -1), null,
                new TestCheckpointManagerMessageHandler());
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void null_ready_handler_throws_argumenbt_null_exception()
        {
            new EmittedStream(
                "test", new EmittedStream.WriterConfiguration(new EmittedStream.WriterConfiguration.StreamMetadata(), null, 50), new ProjectionVersion(1, 0, 0),
                new TransactionFilePositionTagger(0), CheckpointTag.FromPosition(0, 0, -1), _ioDispatcher, null);
        }

        [Test]
        public void it_can_be_created()
        {
            new EmittedStream(
                "test", new EmittedStream.WriterConfiguration(new EmittedStream.WriterConfiguration.StreamMetadata(), null, 50), new ProjectionVersion(1, 0, 0),
                new TransactionFilePositionTagger(0), CheckpointTag.FromPosition(0, 0, -1), _ioDispatcher,
                new TestCheckpointManagerMessageHandler());
        }
    }
}
