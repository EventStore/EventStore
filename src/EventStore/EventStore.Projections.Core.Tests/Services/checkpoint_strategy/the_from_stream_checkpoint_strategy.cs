using System;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.checkpoint_strategy
{
    [TestFixture]
    public class the_from_stream_checkpoint_strategy
    {
        private CheckpointStrategy _strategy;

        [SetUp]
        public void setup()
        {
            var builder = new CheckpointStrategy.Builder();
            builder.FromStream("stream1");
            builder.AllEvents();
            _strategy = builder.Build(ProjectionMode.Persistent);
        }

        [Test]
        public void can_compare_checkpoint_tag_with_event_position()
        {
            Assert.AreEqual(true, _strategy.IsCheckpointTagAfterEventPosition(CheckpointTag.FromStreamPosition("stream1", 100, 150), new EventPosition(100, 30)));
        }

        [Test]
        public void can_compare_checkpoint_tag_with_event_position_negative()
        {
            Assert.AreEqual(false, _strategy.IsCheckpointTagAfterEventPosition(CheckpointTag.FromStreamPosition("stream1", 100, 90), new EventPosition(100, 30)));
        }

        [Test, ExpectedException(typeof(ArgumentException))]
        public void refuses_to_compare_foreigh_checkpoint_tag_with_event_position()
        {
            Assert.AreEqual(true, _strategy.IsCheckpointTagAfterEventPosition(CheckpointTag.FromPosition(100, 50), new EventPosition(100, 30)));
        }


    }
}