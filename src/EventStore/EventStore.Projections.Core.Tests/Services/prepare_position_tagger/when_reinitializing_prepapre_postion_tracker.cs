using System;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.prepare_position_tagger
{
    [TestFixture]
    public class when_reinitializing_prepapre_postion_tracker
    {
        private PositionTagger _tagger;
        private CheckpointTag _tag;
        private PositionTracker _positionTracker;

        [SetUp]
        public void When()
        {
            // given
            var tagger = new PreparePositionTagger();
            var positionTracker = new PositionTracker(tagger);

            var newTag = CheckpointTag.FromPreparePosition(50);
            positionTracker.UpdateByCheckpointTagInitial(newTag);
            _tag = positionTracker.LastTag;
            _tagger = new PreparePositionTagger();
            _positionTracker = new PositionTracker(_tagger);
            _positionTracker.UpdateByCheckpointTagInitial(_tag);
            // when 


            _positionTracker.Initialize();
        }

        [Test]
        public void it_can_be_updated()
        {
            // even not initialized (UpdateToZero can be removed)
            var newTag = CheckpointTag.FromPreparePosition(50);
            _positionTracker.UpdateByCheckpointTagInitial(newTag);
        }

        [Test, ExpectedException(typeof(InvalidOperationException))]
        public void initial_position_cannot_be_set_twice()
        {
            var newTag = CheckpointTag.FromPreparePosition(50);
            _positionTracker.UpdateByCheckpointTagForward(newTag);
            _positionTracker.UpdateByCheckpointTagForward(newTag);
        }

        [Test]
        public void it_can_be_updated_to_zero()
        {
            _positionTracker.UpdateByCheckpointTagInitial(_tagger.MakeZeroCheckpointTag());
        }

        [Test, ExpectedException(typeof(InvalidOperationException))]
        public void it_cannot_be_updated_forward()
        {
            var newTag = CheckpointTag.FromPreparePosition(50);
            _positionTracker.UpdateByCheckpointTagForward(newTag);
        }

    }
}