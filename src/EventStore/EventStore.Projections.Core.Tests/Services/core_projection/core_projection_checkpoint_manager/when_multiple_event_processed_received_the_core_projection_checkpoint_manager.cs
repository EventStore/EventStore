using System;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection.core_projection_checkpoint_manager
{
    [TestFixture]
    public class when_multiple_event_processed_received_the_core_projection_checkpoint_manager : TestFixtureWithCoreProjectionCheckpointManager
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
            }
            catch (Exception ex)
            {
                _exception = ex;
            }
        }

        [Test]
        public void completes_checkpoint()
        {
            Assert.AreEqual(1, _projection._checkpointCompletedMessages.Count);
        }

        [Test]
        public void messages_are_handled()
        {
            Assert.IsNull(_exception);
        }

        [Test]
        public void accepts_stopping()
        {
            _manager.Stopping();
        }

        [Test]
        public void accepts_stopped()
        {
            _manager.Stopped();
        }

        [Test]
        public void accepts_event_processed()
        {
            _manager.EventProcessed(@"{""state"":""state""}", null, CheckpointTag.FromStreamPosition("stream", 13, 1300));
        }

        [Test, ExpectedException(typeof(InvalidOperationException))]
        public void event_processed_at_the_start_position_throws_invalid_operation_exception()
        {
            _manager.EventProcessed(@"{""state"":""state""}", null, CheckpointTag.FromStreamPosition("stream", 10, 1000));
        }

        [Test]
        public void accepts_checkpoint_suggested()
        {
            _manager.CheckpointSuggested(CheckpointTag.FromStreamPosition("stream", 13, 1300));
        }

        [Test, ExpectedException(typeof(InvalidOperationException))]
        public void checkpoint_suggested_at_the_start_position_throws_invalid_operation_exception()
        {
            _manager.CheckpointSuggested(CheckpointTag.FromStreamPosition("stream", 10, 1000));
        }


    }
}