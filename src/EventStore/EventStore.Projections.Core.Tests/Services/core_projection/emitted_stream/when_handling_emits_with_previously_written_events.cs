using System;
using System.Linq;
using EventStore.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection.emitted_stream
{
    [TestFixture]
    public class when_handling_emits_with_previously_written_events : TestFixtureWithExistingEvents
    {
        private EmittedStream _stream;
        private TestCheckpointManagerMessageHandler _readyHandler;

        protected override void Given()
        {
            AllWritesQueueUp();
            ExistingEvent("test_stream", "type1", @"{""CommitPosition"": 100, ""PreparePosition"": 50}", "data");
            ExistingEvent("test_stream", "type2", @"{""CommitPosition"": 200, ""PreparePosition"": 150}", "data");
            ExistingEvent("test_stream", "type3", @"{""CommitPosition"": 300, ""PreparePosition"": 250}", "data");
        }

        [SetUp]
        public void setup()
        {
            _readyHandler = new TestCheckpointManagerMessageHandler();
            _stream = new EmittedStream(
                "test_stream", CheckpointTag.FromPosition(0, -1), CheckpointTag.FromPosition(200, 150), _readDispatcher, _writeDispatcher, _readyHandler,
                maxWriteBatchLength: 50);
            _stream.Start();
        }

        [Test]
        public void does_not_publish_already_published_events()
        {
            _stream.EmitEvents(
                new[] {new EmittedDataEvent("test_stream", Guid.NewGuid(), "type2", "data",
                                            CheckpointTag.FromPosition(200, 150), null)});
            _stream.EmitEvents(
                new[] {new EmittedDataEvent("test_stream", Guid.NewGuid(), "type3", "data",
                                            CheckpointTag.FromPosition(300, 250), null)});
            Assert.AreEqual(0, _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().Count());
        }

        [Test]
        public void publishes_not_yet_published_events()
        {
            _stream.EmitEvents(
                new[] {new EmittedDataEvent("test_stream", Guid.NewGuid(), "type", "data",
                                            CheckpointTag.FromPosition(400, 350), null)});
            Assert.AreEqual(1, _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().Count());
        }

        [Test]
        public void replies_with_write_completed_message_for_existing_events()
        {
            _stream.EmitEvents(
                new[] {new EmittedDataEvent("test_stream", Guid.NewGuid(), "type2", "data",
                                            CheckpointTag.FromPosition(200, 150), null)});
            Assert.AreEqual(1, _readyHandler.HandledWriteCompletedMessage.Count);
        }

        [Test]
        public void retrieves_event_number_for_previously_written_events()
        {
            int eventNumber = -1;
            _stream.EmitEvents(
                new[] {new EmittedDataEvent("test_stream", Guid.NewGuid(), "type2", "data",
                                            CheckpointTag.FromPosition(200, 150), null, v => eventNumber = v)});
            Assert.AreEqual(1, eventNumber);
        }

        [Test]
        public void reply_with_write_completed_message_when_write_completes()
        {
            _stream.EmitEvents(
                new[] {new EmittedDataEvent("test_stream", Guid.NewGuid(), "type", "data",
                                            CheckpointTag.FromPosition(400, 350), null)});
            OneWriteCompletes();
            Assert.IsTrue(_readyHandler.HandledWriteCompletedMessage.Any(v => v.StreamId == "test_stream")); // more than one is ok
        }

        [Test]
        public void reports_event_number_for_new_events()
        {
            int eventNumber = -1;
            _stream.EmitEvents(
                new[] {new EmittedDataEvent("test_stream", Guid.NewGuid(), "type", "data",
                                            CheckpointTag.FromPosition(400, 350), null, v => eventNumber = v)});
            OneWriteCompletes();
            Assert.AreEqual(3, eventNumber);
        }

    }
}