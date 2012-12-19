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
using System.Collections.Generic;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection.checkpoint_manager.multi_stream
{
    [TestFixture]
    public class when_multiple_event_processed_received : TestFixtureWithCoreProjectionMultiStreamCheckpointManager
    {
        private Exception _exception;

        protected override void Given()
        {
            AllWritesSucceed();
            base.Given();
            _checkpointHandledThreshold = 2;
        }

        protected override void When()
        {
            base.When();
            _exception = null;
            try
            {
                _manager.BeginLoadState();
                _manager.Start(CheckpointTag.FromStreamPositions(new Dictionary<string, int> {{"a", 2}, {"b", 2}}));
//                _manager.UpdateState("", @"{""state"":""state1""}");
                _manager.EventProcessed(CheckpointTag.FromStreamPositions(new Dictionary<string, int> {{"a", 2}, {"b", 3}}), 77.7f);
//                _manager.UpdateState("", @"{""state"":""state2""}");
                _manager.EventProcessed(CheckpointTag.FromStreamPositions(new Dictionary<string, int> {{"a", 2}, {"b", 4}}), 77.7f);
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
//            _manager.UpdateState("", @"{""state"":""state""}");
            _manager.EventProcessed(CheckpointTag.FromStreamPositions(new Dictionary<string, int> {{"a", 3}, {"b", 4}}), 77.7f);
        }

        [Test, ExpectedException(typeof (InvalidOperationException))]
        public void event_processed_at_the_start_position_throws_invalid_operation_exception()
        {
//            _manager.UpdateState("", @"{""state"":""state""}");
            _manager.EventProcessed(CheckpointTag.FromStreamPositions(new Dictionary<string, int> {{"a", 2}, {"b", 2}}), 77.7f);
        }

        [Test]
        public void accepts_checkpoint_suggested()
        {
            _manager.CheckpointSuggested(
                CheckpointTag.FromStreamPositions(new Dictionary<string, int> {{"a", 3}, {"b", 6}}), 77.7f);
        }

        [Test, ExpectedException(typeof (InvalidOperationException))]
        public void checkpoint_suggested_at_the_start_position_throws_invalid_operation_exception()
        {
            _manager.CheckpointSuggested(
                CheckpointTag.FromStreamPositions(new Dictionary<string, int> {{"a", 2}, {"b", 2}}), 77.7f);
        }
        //TODO: load state tests (Default checkpoint manager as well)
    }
}
