// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

using System;
using System.Linq;
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
                _manager.BeginLoadState();
                _manager.BeginLoadPrerecordedEvents(
                    _consumer.HandledMessages.OfType<CoreProjectionProcessingMessage.CheckpointLoaded>()
                        .First()
                        .CheckpointTag);
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

        [Test, ExpectedException(typeof(InvalidOperationException))]
        public void start_throws_invalid_operation_exception()
        {
            _manager.Start(CheckpointTag.FromStreamPosition(0, "stream", 10));
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

        [Test, ExpectedException(typeof(InvalidOperationException))]
        public void event_processed_at_the_start_position_throws_invalid_operation_exception()
        {
//            _manager.StateUpdated("", @"{""state"":""state""}");
            _manager.EventProcessed(CheckpointTag.FromStreamPosition(0, "stream", 10), 77.7f);
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
