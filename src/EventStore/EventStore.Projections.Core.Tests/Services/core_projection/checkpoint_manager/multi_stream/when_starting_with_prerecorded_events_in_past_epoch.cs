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

using System.Collections.Generic;
using System.Linq;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection.checkpoint_manager.multi_stream
{
    [TestFixture]
    public class when_starting_with_prerecorded_events_in_past_epoch : TestFixtureWithMultiStreamCheckpointManager
    {
        private readonly CheckpointTag _tag1 =
            CheckpointTag.FromStreamPositions(0, new Dictionary<string, int> {{"a", 0}, {"b", 0}, {"c", 1}});

        private readonly CheckpointTag _tag2 =
            CheckpointTag.FromStreamPositions(0, new Dictionary<string, int> {{"a", 1}, {"b", 0}, {"c", 1}});

        //private readonly CheckpointTag _tag3 =CheckpointTag.FromStreamPositions(new Dictionary<string, int> {{"a", 1}, {"b", 1}, {"c", 1}});

        protected override void Given()
        {
            base.Given();
            _projectionVersion = new ProjectionVersion(1, 2, 2);
            ExistingEvent(
                "$projections-projection-checkpoint", "$ProjectionCheckpoint", @"{""v"":2, ""s"": {""a"": 0, ""b"": 0, ""c"": 0}}",
                "{}");
            ExistingEvent("a", "StreamCreated", "", "");
            ExistingEvent("b", "StreamCreated", "", "");
            ExistingEvent("c", "StreamCreated", "", "");

            ExistingEvent("a", "Event", "", @"{""data"":""a""");
            ExistingEvent("b", "Event", "", @"{""data"":""b""");
            ExistingEvent("c", "Event", "", @"{""data"":""c""");

            ExistingEvent("$projections-projection-order", "$>", @"{""v"":1, ""s"": {""a"": 0, ""b"": 0, ""c"": 0}}", "0@c");
            ExistingEvent("$projections-projection-order", "$>", @"{""v"":1, ""s"": {""a"": 0, ""b"": 0, ""c"": 1}}", "1@c");
            ExistingEvent("$projections-projection-order", "$>", @"{""v"":1, ""s"": {""a"": 1, ""b"": 0, ""c"": 1}}", "1@a");
            ExistingEvent("$projections-projection-order", "$>", @"{""v"":1, ""s"": {""a"": 1, ""b"": 1, ""c"": 1}}", "1@b");
            ExistingEvent("$projections-projection-order", "$>", @"{""v"":2, ""s"": {""a"": 0, ""b"": 0, ""c"": 0}}", "0@c");
            ExistingEvent("$projections-projection-order", "$>", @"{""v"":2, ""s"": {""a"": 0, ""b"": 0, ""c"": 1}}", "1@c");
            ExistingEvent("$projections-projection-order", "$>", @"{""v"":2, ""s"": {""a"": 1, ""b"": 0, ""c"": 1}}", "1@a");
        }

        protected override void When()
        {
            base.When();
            _manager.BeginLoadState();
            _manager.BeginLoadPrerecordedEvents(
                _consumer.HandledMessages.OfType<CoreProjectionProcessingMessage.CheckpointLoaded>()
                    .First()
                    .CheckpointTag);
        }

        [Test]
        public void sends_correct_checkpoint_loaded_message()
        {
            Assert.AreEqual(1, _projection._checkpointLoadedMessages.Count);
            Assert.AreEqual(
                CheckpointTag.FromStreamPositions(0, new Dictionary<string, int> {{"a", 0}, {"b", 0}, {"c", 0}}),
                _projection._checkpointLoadedMessages.Single().CheckpointTag);
            Assert.AreEqual("{}", _projection._checkpointLoadedMessages.Single().CheckpointData);
        }

        [Test]
        public void sends_correct_prerecorded_events_loaded_message()
        {
            Assert.AreEqual(1, _projection._prerecordedEventsLoadedMessages.Count);
            Assert.AreEqual(
                CheckpointTag.FromStreamPositions(0, new Dictionary<string, int> {{"a", 1}, {"b", 0}, {"c", 1}}),
                _projection._prerecordedEventsLoadedMessages.Single().CheckpointTag);
        }

        [Test]
        public void sends_committed_event_received_messages_in_correct_order()
        {
            var messages = HandledMessages.OfType<EventReaderSubscriptionMessage.CommittedEventReceived>().ToList();
            Assert.AreEqual(2, messages.Count);

            var message1 = messages[0];
            var message2 = messages[1];

            Assert.AreEqual(_tag1, message1.CheckpointTag);
            Assert.AreEqual(_tag2, message2.CheckpointTag);
        }

    }
}
