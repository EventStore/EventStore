using System;
using System.Collections.Generic;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection.checkpoint_manager
{
    [TestFixture]
    public class when_beginning_to_load_state_the_core_projection_checkpoint_manager : TestFixtureWithCoreProjectionCheckpointManager
    {
        private Exception _exception;

        protected override void When()
        {
            base.When();
            _exception = null;
            try
            {
                _manager.BeginLoadState();
            }
            catch (Exception ex)
            {
                _exception = ex;
            }
        }

        [Test]
        public void it_can_be_invoked()
        {
            Assert.IsNull(_exception);
        }

        [Test, ExpectedException(typeof(InvalidOperationException))]
        public void start_throws_invalid_operation_exception()
        {
            _manager.BeginLoadState();
        }

        [Test, ExpectedException(typeof(InvalidOperationException))]
        public void stopping_throws_invalid_operation_exception()
        {
            _manager.Stopping();
        }

        [Test, ExpectedException(typeof(InvalidOperationException))]
        public void stopped_throws_invalid_operation_exception()
        {
            _manager.Stopped();
        }

        [Test, ExpectedException(typeof(InvalidOperationException))]
        public void request_checkpoint_to_stop_throws_invalid_operation_exception()
        {
            _manager.RequestCheckpointToStop();
        }

        [Test, ExpectedException(typeof(InvalidOperationException))]
        public void event_processed_throws_invalid_operation_exception()
        {
            _manager.EventProcessed(@"{""state"":""state""}", CheckpointTag.FromStreamPosition("stream", 10), 77.7f);
        }

        [Test, ExpectedException(typeof(InvalidOperationException))]
        public void checkpoint_suggested_throws_invalid_operation_exception()
        {
            _manager.CheckpointSuggested(CheckpointTag.FromStreamPosition("stream", 10), 77.7f);
        }

        [Test, ExpectedException(typeof(InvalidOperationException))]
        public void ready_for_checkpoint_throws_invalid_operation_exception()
        {
            _manager.Handle(new CoreProjectionProcessingMessage.ReadyForCheckpoint(null));
        }

        [Test]
        public void can_be_started()
        {
            _manager.Start(CheckpointTag.FromStreamPosition("stream", 10));
        }

        [Test, ExpectedException(typeof(InvalidOperationException))]
        public void cannot_be_started_from_incompatible_checkpoint_tag()
        {
            //TODO: move to when loaded
            _manager.Start(CheckpointTag.FromStreamPosition("stream1", 10));
        }
    }
}