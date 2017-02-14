using System;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection.checkpoint_manager
{
    [TestFixture]
    public class when_starting_the_core_projection_checkpoint_manager : TestFixtureWithCoreProjectionCheckpointManager
    {
        private Exception _exception;

        protected override void Given()
        {
            base.Given();
            AllWritesSucceed();
        }

        protected override void When()
        {
            base.When();
            _exception = null;
            try
            {
                _checkpointReader.BeginLoadState();
                var checkpointLoaded =
                    _consumer.HandledMessages.OfType<CoreProjectionProcessingMessage.CheckpointLoaded>().First();
                _checkpointWriter.StartFrom(checkpointLoaded.CheckpointTag, checkpointLoaded.CheckpointEventNumber);
                _manager.BeginLoadPrerecordedEvents(checkpointLoaded.CheckpointTag);

                _manager.Start(CheckpointTag.FromStreamPosition(0, "stream", 10));
            }
            catch (Exception ex)
            {
                _exception = ex;
            }
        }

        [Test]
        public void it_can_be_started()
        {
            Assert.IsNull(_exception);
        }

        [Test]
        public void start_throws_invalid_operation_exception()
        {
            Assert.Throws<InvalidOperationException>(()=> { _manager.Start(CheckpointTag.FromStreamPosition(0, "stream", 10)); });
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
//            _manager.StateUpdated("", @"{""state"":""state""}");
            _manager.EventProcessed(CheckpointTag.FromStreamPosition(0, "stream", 11), 77.7f);
        }

        [Test]
        public void event_processed_at_the_start_position_throws_invalid_operation_exception()
        {
//            _manager.StateUpdated("", @"{""state"":""state""}");
            Assert.Throws<InvalidOperationException>(()=> { _manager.EventProcessed(CheckpointTag.FromStreamPosition(0, "stream", 10), 77.7f); });
        }

        [Test]
        public void accepts_checkpoint_suggested()
        {
            _manager.CheckpointSuggested(CheckpointTag.FromStreamPosition(0, "stream", 11), 77.7f);
            Assert.AreEqual(1, _projection._checkpointCompletedMessages.Count);
        }

        [Test]
        public void accepts_checkpoint_suggested_even_at_the_start_position_but_does_not_complete_it()
        {
            _manager.CheckpointSuggested(CheckpointTag.FromStreamPosition(0, "stream", 10), 77.7f);
            Assert.AreEqual(0, _projection._checkpointCompletedMessages.Count);
        }


    }
}
