﻿using System;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.position_tagging.transaction_file_position_tagger
{
    [TestFixture]
    public class when_reinitializing_transaction_file_postion_tracker
    {
        private PositionTagger _tagger;
        private CheckpointTag _tag;
        private PositionTracker _positionTracker;

        [SetUp]
        public void When()
        {
            // given
            var tagger = new TransactionFilePositionTagger(0);
            var positionTracker = new PositionTracker(tagger);

            var newTag = CheckpointTag.FromPosition(0, 100, 50);
            positionTracker.UpdateByCheckpointTagInitial(newTag);
            _tag = positionTracker.LastTag;
            _tagger = new TransactionFilePositionTagger(0);
            _positionTracker = new PositionTracker(_tagger);
            _positionTracker.UpdateByCheckpointTagInitial(_tag);
            // when 


            _positionTracker.Initialize();
        }

        [Test]
        public void it_can_be_updated()
        {
            // even not initialized (UpdateToZero can be removed)
            var newTag = CheckpointTag.FromPosition(0, 100, 50);
            _positionTracker.UpdateByCheckpointTagInitial(newTag);
        }

        [Test]
        public void initial_position_cannot_be_set_twice()
        {
            Assert.Throws<InvalidOperationException>(() => {
            var newTag = CheckpointTag.FromPosition(0, 100, 50);
            _positionTracker.UpdateByCheckpointTagForward(newTag);
            _positionTracker.UpdateByCheckpointTagForward(newTag);
            });
        }

        [Test]
        public void it_can_be_updated_to_zero()
        {
            _positionTracker.UpdateByCheckpointTagInitial(_tagger.MakeZeroCheckpointTag());
        }

        [Test]
        public void it_cannot_be_updated_forward()
        {
            Assert.Throws<InvalidOperationException>(() => {
            var newTag = CheckpointTag.FromPosition(0, 100, 50);
            _positionTracker.UpdateByCheckpointTagForward(newTag);
            });
        }

    }
}