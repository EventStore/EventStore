using System;
using System.Collections.Generic;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.position_tagging.multistream_position_tagger
{
    [TestFixture]
    public class when_creating_multistream_postion_tracker
    {
        private MultiStreamPositionTagger _tagger;
        private PositionTracker _positionTracker;

        [SetUp]
        public void when()
        {
            _tagger = new MultiStreamPositionTagger(0, new []{"stream1", "stream2"});
            _positionTracker = new PositionTracker(_tagger);
        }

        [Test]
        public void it_can_be_updated_with_correct_streams()
        {
            // even not initialized (UpdateToZero can be removed)
            var newTag = CheckpointTag.FromStreamPositions(0, new Dictionary<string, int>{{"stream1", 10}, {"stream2", 20}});
            _positionTracker.UpdateByCheckpointTagInitial(newTag);
        }

        [Test]
        public void it_cannot_be_updated_with_other_streams()
        {
            Assert.Throws<InvalidOperationException>(() => {
            var newTag = CheckpointTag.FromStreamPositions(0, new Dictionary<string, int> { { "stream1", 10 }, { "stream3", 20 } });
            _positionTracker.UpdateByCheckpointTagInitial(newTag);
            });
        }

        [Test]
        public void it_cannot_be_updated_forward()
        {
            Assert.Throws<InvalidOperationException>(() => {
            var newTag = CheckpointTag.FromStreamPositions(0, new Dictionary<string, int> { { "stream1", 10 }, { "stream2", 20 } });
            _positionTracker.UpdateByCheckpointTagForward(newTag);
            });
        }

        [Test]
        public void initial_position_cannot_be_set_twice()
        {
            Assert.Throws<InvalidOperationException>(() => {
            var newTag = CheckpointTag.FromStreamPositions(0, new Dictionary<string, int> { { "stream1", 10 }, { "stream2", 20 } });
            _positionTracker.UpdateByCheckpointTagForward(newTag);
            _positionTracker.UpdateByCheckpointTagForward(newTag);
            });
        }

        [Test]
        public void it_can_be_updated_to_zero()
        {
            _positionTracker.UpdateByCheckpointTagInitial(_tagger.MakeZeroCheckpointTag());
        }
    }
}
