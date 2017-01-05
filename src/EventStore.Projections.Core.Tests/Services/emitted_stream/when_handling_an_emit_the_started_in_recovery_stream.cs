﻿using System;
using System.Linq;
using EventStore.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.emitted_stream
{
    [TestFixture]
    public class when_handling_an_emit_the_started_in_recovery_stream : TestFixtureWithExistingEvents
    {
        private EmittedStream _stream;
        private TestCheckpointManagerMessageHandler _readyHandler;

        protected override void Given()
        {
            AllWritesQueueUp();
            ExistingEvent("test_stream", "type", @"{""c"": 100, ""p"": 50}", "data");
        }

        [SetUp]
        public void setup()
        {
            _readyHandler = new TestCheckpointManagerMessageHandler();
            _stream = new EmittedStream(
                "test_stream", new EmittedStream.WriterConfiguration(new EmittedStream.WriterConfiguration.StreamMetadata(), null, maxWriteBatchLength: 50),
                new ProjectionVersion(1, 0, 0), new TransactionFilePositionTagger(0), CheckpointTag.FromPosition(0, 40, 30),
                _ioDispatcher, _readyHandler);
            _stream.Start();
        }

        [Test]
        public void throws_if_position_is_prior_to_from_position()
        {
            Assert.Throws<InvalidOperationException>(() => {
            _stream.EmitEvents(
                new[]
                {
                    new EmittedDataEvent(
                        "test_stream", Guid.NewGuid(), "type", true, "data", null, CheckpointTag.FromPosition(0, 20, 10), null)
                });
            });
        }

        [Test]
        public void does_not_publish_already_published_events()
        {
            _stream.EmitEvents(
                new[]
                {
                    new EmittedDataEvent(
                        "test_stream", Guid.NewGuid(), "type", true, "data", null, CheckpointTag.FromPosition(0, 100, 50), null)
                });
            Assert.AreEqual(0, _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().Count());
        }

        [Test]
        public void publishes_not_yet_published_events()
        {
            _stream.EmitEvents(
                new[]
                {
                    new EmittedDataEvent(
                        "test_stream", Guid.NewGuid(), "type", true, "data", null, CheckpointTag.FromPosition(0, 200, 150), null)
                });
            Assert.AreEqual(1, _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().Count());
        }

        [Test]
        public void does_not_reply_with_write_completed_message()
        {
            _stream.EmitEvents(
                new[]
                {
                    new EmittedDataEvent(
                        "test_stream", Guid.NewGuid(), "type", true, "data", null, CheckpointTag.FromPosition(0, 200, 150), null)
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
                        "test_stream", Guid.NewGuid(), "type", true, "data", null, CheckpointTag.FromPosition(0, 200, 150), null)
                });
            OneWriteCompletes();
            Assert.IsTrue(_readyHandler.HandledWriteCompletedMessage.Any(v => v.StreamId == "test_stream"));
                // more than one is ok
        }
    }
}
