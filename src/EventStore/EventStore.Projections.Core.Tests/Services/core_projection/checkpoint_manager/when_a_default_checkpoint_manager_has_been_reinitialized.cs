using System;
using System.Collections.Generic;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection.checkpoint_manager
{
    [TestFixture]
    public class when_a_default_checkpoint_manager_has_been_reinitialized : TestFixtureWithCoreProjectionCheckpointManager
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
                _manager.BeginLoadState();
                _manager.Start(CheckpointTag.FromStreamPosition("stream", 10));
//                _manager.StateUpdated("", @"{""state"":""state1""}");
                _manager.EventProcessed(CheckpointTag.FromStreamPosition("stream", 11), 77.7f);
//                _manager.StateUpdated("", @"{""state"":""state2""}");
                _manager.EventProcessed(CheckpointTag.FromStreamPosition("stream", 12), 77.7f);
                _manager.Initialize();
            }
            catch (Exception ex)
            {
                _exception = ex;
            }
        }



        [Test, ExpectedException(typeof (InvalidOperationException))]
        public void stopping_throws_invalid_operation_exception()
        {
            _manager.Stopping();
        }

        [Test, ExpectedException(typeof (InvalidOperationException))]
        public void stopped_throws_invalid_operation_exception()
        {
            _manager.Stopped();
        }

        [Test, ExpectedException(typeof(InvalidOperationException))]
        public void event_processed_throws_invalid_operation_exception()
        {
//            _manager.StateUpdated("", @"{""state"":""state""}");
            _manager.EventProcessed(CheckpointTag.FromStreamPosition("stream", 10), 77.7f);
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

        [Test, ExpectedException(typeof(InvalidOperationException))]
        public void starts_throws_invalid_operation_exception()
        {
            _manager.Start(CheckpointTag.FromStreamPosition("stream", 10));
        }

        [Test]
        public void can_begin_load_state()
        {
            _manager.BeginLoadState();
        }

    }
}