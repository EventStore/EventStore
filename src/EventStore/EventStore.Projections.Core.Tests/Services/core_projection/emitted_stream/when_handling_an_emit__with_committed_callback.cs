using System;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection.emitted_stream
{
    [TestFixture]
    public class when_handling_an_emit__with_committed_callback : TestFixtureWithExistingEvents
    {
        private EmittedStream _stream;
        private TestCheckpointManagerMessageHandler _readyHandler;

        protected override void Given()
        {
            ExistingEvent("test_stream", "type", @"{""CommitPosition"": 100, ""PreparePosition"": 50}", "data");
            AllWritesSucceed();
        }

        [SetUp]
        public void setup()
        {
            _readyHandler = new TestCheckpointManagerMessageHandler();
            _stream = new EmittedStream(
                "test_stream", CheckpointTag.FromPosition(0, -1), CheckpointTag.FromPosition(0, -1), _readDispatcher, _writeDispatcher, _readyHandler,
                maxWriteBatchLength: 50);
            _stream.Start();
        }

        [Test]
        public void completes_already_published_events()
        {
            var invoked = false;
            _stream.EmitEvents(
                new[]
                    {
                        new EmittedEvent(
                    "test_stream", Guid.NewGuid(), "type", "data", CheckpointTag.FromPosition(100, 50), null, () => invoked = true)
                    });
            Assert.IsTrue(invoked);
        }

        [Test]
        public void completes_not_yet_published_events()
        {
            var invoked = false;
            _stream.EmitEvents(
                new[]
                    {
                        new EmittedEvent(
                    "test_stream", Guid.NewGuid(), "type", "data", CheckpointTag.FromPosition(200, 150), null, () => invoked = true)
                    });
            Assert.IsTrue(invoked);
        }
    }
}
