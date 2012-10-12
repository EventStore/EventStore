using System;
using EventStore.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;
using System.Linq;

namespace EventStore.Projections.Core.Tests.Services.core_projection.core_projection_checkpoint_manager
{
    [TestFixture]
    public class when_a_checkpoint_has_been_completed_and_requesting_checkpoint_to_stop_the_core_projection_checkpoint_manager : TestFixtureWithCoreProjectionCheckpointManager
    {
        private Exception _exception;

        protected override void Given()
        {
            AllWritesSucceed();
            base.Given();
            this._checkpointHandledThreshold = 2;
        }

        protected override void When()
        {
            base.When();
            _exception = null;
            try
            {
                _manager.Start(CheckpointTag.FromStreamPosition("stream", 10, 1000), 5);
                _manager.EventProcessed(@"{""state"":""state1""}", null, CheckpointTag.FromStreamPosition("stream", 11, 1100));
                _manager.EventProcessed(@"{""state"":""state2""}", null, CheckpointTag.FromStreamPosition("stream", 12, 1200));
                _manager.Stopping();
                _manager.RequestCheckpointToStop();
            }
            catch (Exception ex)
            {
                _exception = ex;
            }
        }

        [Test]
        public void two_checkpoints_are_completed()
        {
            Assert.AreEqual(2, _projection._checkpointCompletedMessages.Count);
        }


        [Test]
        public void only_one_checkpoint_has_been_written()
        {
            Assert.AreEqual(1, _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().Count());
        }


    }
}