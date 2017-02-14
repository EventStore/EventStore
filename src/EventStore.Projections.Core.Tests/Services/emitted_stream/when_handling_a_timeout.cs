﻿using System;
using System.Linq;
using EventStore.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;
using System.Collections.Generic;

namespace EventStore.Projections.Core.Tests.Services.emitted_stream.another_epoch
{
    [TestFixture]
    public class when_handling_a_timeout : TestFixtureWithExistingEvents
    {
        private EmittedStream _stream;
        private TestCheckpointManagerMessageHandler _readyHandler;

        protected override void Given()
        {
            AllWritesQueueUp();
            ExistingEvent("test_stream", "type1", @"{""v"": 1, ""c"": 100, ""p"": 50}", "data");
            ExistingEvent("test_stream", "type1", @"{""v"": 2, ""c"": 100, ""p"": 50}", "data");
        }

        private EmittedEvent[] CreateEventBatch()
        {
            return new EmittedEvent[]
            {
                new EmittedDataEvent(
                    (string) "test_stream", Guid.NewGuid(), (string) "type1", (bool) true,
                    (string) "data", (ExtraMetaData) null, CheckpointTag.FromPosition(0, 100, 50), (CheckpointTag) null, null),
                new EmittedDataEvent(
                    (string) "test_stream", Guid.NewGuid(), (string) "type2", (bool) true,
                    (string) "data", (ExtraMetaData) null, CheckpointTag.FromPosition(0, 100, 50), (CheckpointTag) null, null),
                new EmittedDataEvent(
                    (string) "test_stream", Guid.NewGuid(), (string) "type3", (bool) true,
                    (string) "data", (ExtraMetaData) null, CheckpointTag.FromPosition(0, 100, 50), (CheckpointTag) null, null)
            };
        }

        [SetUp]
        public void setup()
        {
            _readyHandler = new TestCheckpointManagerMessageHandler();
            _stream = new EmittedStream(
                "test_stream", new EmittedStream.WriterConfiguration(new EmittedStream.WriterConfiguration.StreamMetadata(), null, maxWriteBatchLength: 50),
                new ProjectionVersion(1, 2, 2), new TransactionFilePositionTagger(0), CheckpointTag.Empty,
                _ioDispatcher, _readyHandler);
            _stream.Start();
            _stream.EmitEvents(CreateEventBatch());

            CompleteWriteWithResult(OperationResult.CommitTimeout);
        }

        [Test]
        public void should_retry_the_write_with_the_same_events()
        {
            var current = _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().Last();
            while (_consumer.HandledMessages.Last().GetType() == typeof(EventStore.Core.Services.TimerService.TimerMessage.Schedule))
            {
                var message = _consumer.HandledMessages.Last() as EventStore.Core.Services.TimerService.TimerMessage.Schedule;
                message.Envelope.ReplyWith(message.ReplyMessage);

                CompleteWriteWithResult(OperationResult.CommitTimeout);

                var last = _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().Last();

                Assert.AreEqual(current.EventStreamId, last.EventStreamId);
                Assert.AreEqual(current.Events, last.Events);
                
                current = last;
            }

            Assert.AreEqual(1, _readyHandler.HandledFailedMessages.OfType<CoreProjectionProcessingMessage.Failed>().Count(), "Should fail the projection after exhausting all the write retries");
        }
    }
}
