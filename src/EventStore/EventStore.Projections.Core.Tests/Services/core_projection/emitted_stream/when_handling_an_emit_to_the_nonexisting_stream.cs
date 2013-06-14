using System;
using System.Linq;
using EventStore.Core.Messages;
using EventStore.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection.emitted_stream
{
    [TestFixture]
    public class when_handling_an_emit_to_the_nonexisting_stream : TestFixtureWithExistingEvents
    {
        private EmittedStream _stream;
        private TestCheckpointManagerMessageHandler _readyHandler;

        protected override void Given()
        {
            AllWritesQueueUp();
            AllWritesToSucceed("$$test_stream");
            NoOtherStreams();
        }

        [SetUp]
        public void setup()
        {
            _readyHandler = new TestCheckpointManagerMessageHandler();
            _stream = new EmittedStream(
                "test_stream", new ProjectionVersion(1, 0, 0), null, new TransactionFilePositionTagger(),
                CheckpointTag.FromPosition(0, -1), CheckpointTag.FromPosition(40, 30), _readDispatcher, _writeDispatcher,
                _readyHandler, maxWriteBatchLength: 50);
            _stream.Start();
        }

        [Test, ExpectedException(typeof(InvalidOperationException))]
        public void throws_if_position_is_prior_to_from_position()
        {
            _stream.EmitEvents(
                new[]
                    {
                        new EmittedDataEvent(
                    "test_stream", Guid.NewGuid(), "type", "data", null, CheckpointTag.FromPosition(20, 10), null)
                    });
        }

        [Test]
        public void publishes_already_published_events()
        {
            _stream.EmitEvents(
                new[]
                    {
                        new EmittedDataEvent(
                    "test_stream", Guid.NewGuid(), "type", "data", null, CheckpointTag.FromPosition(100, 50), null)
                    });
            Assert.AreEqual(
                1,
                _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>()
                         .ExceptOfEventType(SystemEventTypes.StreamMetadata)
                         .Count());
        }

        [Test]
        public void publishes_not_yet_published_events()
        {
            _stream.EmitEvents(
                new[]
                    {
                        new EmittedDataEvent(
                    "test_stream", Guid.NewGuid(), "type", "data", null, CheckpointTag.FromPosition(200, 150), null)
                    });
            Assert.AreEqual(
                1,
                _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>()
                         .ExceptOfEventType(SystemEventTypes.StreamMetadata)
                         .Count());
        }

        [Test]
        public void does_not_reply_with_write_completed_message()
        {
            _stream.EmitEvents(
                new[]
                    {
                        new EmittedDataEvent(
                    "test_stream", Guid.NewGuid(), "type", "data", null, CheckpointTag.FromPosition(200, 150), null)
                    });
            Assert.AreEqual(0, _readyHandler.HandledWriteCompletedMessage.Count);
        }

        [Test]
        public void reply_with_write_completed_message_when_write_completes()
        {
            _stream.EmitEvents(
                new[]
                    {
                        new EmittedDataEvent(
                    "test_stream", Guid.NewGuid(), "type", "data", null, CheckpointTag.FromPosition(200, 150), null)
                    });
            OneWriteCompletes();
            Assert.IsTrue(_readyHandler.HandledWriteCompletedMessage.Any(v => v.StreamId == "test_stream")); // more than one is ok
        }

    }
}