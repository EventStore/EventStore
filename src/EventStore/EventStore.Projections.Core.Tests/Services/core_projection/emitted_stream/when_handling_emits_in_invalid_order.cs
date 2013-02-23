using System;
using System.Linq;
using EventStore.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection.emitted_stream
{
    [TestFixture]
    public class when_handling_emits_in_invalid_order : TestFixtureWithExistingEvents
    {
        private EmittedStream _stream;
        private TestCheckpointManagerMessageHandler _readyHandler;

        protected override void Given()
        {
            ExistingEvent("test_stream", "type", @"{""CommitPosition"": 100, ""PreparePosition"": 50}", "data");
        }

        [SetUp]
        public void setup()
        {
            _readyHandler = new TestCheckpointManagerMessageHandler();
            _stream = new EmittedStream(
                "test_stream", CheckpointTag.FromPosition(0, -1), CheckpointTag.FromPosition(40, 30), _readDispatcher, _writeDispatcher, _readyHandler,
                maxWriteBatchLength: 50);
            _stream.Start();
            _stream.EmitEvents(
                new[] {new EmittedDataEvent("test_stream", Guid.NewGuid(), "type", "data",
                                        CheckpointTag.FromPosition(100, 90), null)});
        }

        [Test, ExpectedException(typeof(InvalidOperationException))]
        public void throws_if_position_is_prior_to_the_last_event_position()
        {
            _stream.EmitEvents(
                new[] {new EmittedDataEvent("test_stream", Guid.NewGuid(), "type", "data",
                                        CheckpointTag.FromPosition(80, 70), null)});
        }

    }
}