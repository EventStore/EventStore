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
using System.Text;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.position_tagging.stream_position_tagger
{
    [TestFixture]
    public class stream_position_tagger
    {
        private ReaderSubscriptionMessage.CommittedEventDistributed _zeroEvent;
        private ReaderSubscriptionMessage.CommittedEventDistributed _firstEvent;
        private ReaderSubscriptionMessage.CommittedEventDistributed _secondEvent;

        [SetUp]
        public void setup()
        {
            _zeroEvent = ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
                Guid.NewGuid(), new TFPos(10, 0), "stream1", 0, false, Guid.NewGuid(), "StreamCreated", false,
                new byte[0], new byte[0]);
            _firstEvent = ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
                Guid.NewGuid(), new TFPos(30, 20), "stream1", 1, false, Guid.NewGuid(), "Data", true,
                Helper.UTF8NoBom.GetBytes("{}"), new byte[0]);
            _secondEvent = ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
                Guid.NewGuid(), new TFPos(50, 40), "stream1", 2, false, Guid.NewGuid(), "Data", true,
                Helper.UTF8NoBom.GetBytes("{}"), new byte[0]);
        }

        [Test]
        public void can_be_created()
        {
            var t = new StreamPositionTagger(0, "stream1");
            new PositionTracker(t);
        }

        [Test]
        public void is_message_after_checkpoint_tag_after_case()
        {
            var t = new StreamPositionTagger(0, "stream1");
            var result = t.IsMessageAfterCheckpointTag(CheckpointTag.FromStreamPosition(0, "stream1", 0), _firstEvent);
            Assert.IsTrue(result);
        }

        [Test]
        public void is_message_after_checkpoint_tag_before_case()
        {
            var t = new StreamPositionTagger(0, "stream1");
            var result = t.IsMessageAfterCheckpointTag(CheckpointTag.FromStreamPosition(0, "stream1", 2), _firstEvent);
            Assert.IsFalse(result);
        }

        [Test]
        public void is_message_after_checkpoint_tag_equal_case()
        {
            var t = new StreamPositionTagger(0, "stream1");
            var result = t.IsMessageAfterCheckpointTag(CheckpointTag.FromStreamPosition(0, "stream1", 1), _firstEvent);
            Assert.IsFalse(result);
        }

        [Test]
        public void is_message_after_checkpoint_tag_incompatible_case()
        {
            // events from other streams are not after any tag
            var t = new StreamPositionTagger(0, "stream-other");
            var result = t.IsMessageAfterCheckpointTag(CheckpointTag.FromStreamPosition(0, "stream1", 1), _firstEvent);
            Assert.IsFalse(result);
        }


        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void null_stream_throws_argument_null_exception()
        {
            new StreamPositionTagger(0, null);
        }

        [Test, ExpectedException(typeof (ArgumentException))]
        public void empty_stream_throws_argument_exception()
        {
            new StreamPositionTagger(0, "");
        }

        [Test]
        public void position_checkpoint_tag_is_incompatible()
        {
            var t = new StreamPositionTagger(0, "stream1");
            Assert.IsFalse(t.IsCompatible(CheckpointTag.FromPosition(0, 1000, 500)));
        }

        [Test]
        public void anothe_stream_checkpoint_tag_is_incompatible()
        {
            var t = new StreamPositionTagger(0, "stream1");
            Assert.IsFalse(t.IsCompatible(CheckpointTag.FromStreamPosition(0, "stream2", 100)));
        }

        [Test]
        public void the_same_stream_checkpoint_tag_is_compatible()
        {
            var t = new StreamPositionTagger(0, "stream1");
            Assert.IsTrue(t.IsCompatible(CheckpointTag.FromStreamPosition(0, "stream1", 100)));
        }

        [Test]
        public void adjust_compatible_tag_returns_the_same_tag()
        {
            var t = new StreamPositionTagger(0, "stream1");
            var tag = CheckpointTag.FromStreamPosition(0, "stream1", 1);
            Assert.AreEqual(tag, t.AdjustTag(tag));
        }

        [Test]
        public void can_adjust_multi_stream_position_tag()
        {
            var t = new StreamPositionTagger(0, "stream1");
            var tag = CheckpointTag.FromStreamPosition(0, "stream1", 1);
            var original = CheckpointTag.FromStreamPositions(0, new Dictionary<string, int> {{"stream1", 1}, {"stream2", 2}});
            Assert.AreEqual(tag, t.AdjustTag(original));
        }

        [Test]
        public void zero_position_tag_is_before_first_event_possible()
        {
            var t = new StreamPositionTagger(0, "stream1");
            var zero = t.MakeZeroCheckpointTag();

            var zeroFromEvent = t.MakeCheckpointTag(zero, _zeroEvent);

            Assert.IsTrue(zeroFromEvent > zero);
        }

        [Test]
        public void produced_checkpoint_tags_are_correctly_ordered()
        {
            var t = new StreamPositionTagger(0, "stream1");
            var zero = t.MakeZeroCheckpointTag();

            var zeroEvent = t.MakeCheckpointTag(zero, _zeroEvent);
            var zeroEvent2 = t.MakeCheckpointTag(zeroEvent, _zeroEvent);
            var first = t.MakeCheckpointTag(zeroEvent2, _firstEvent);
            var second = t.MakeCheckpointTag(first, _secondEvent);
            var second2 = t.MakeCheckpointTag(zero, _secondEvent);

            Assert.IsTrue(zeroEvent > zero);
            Assert.IsTrue(first > zero);
            Assert.IsTrue(second > first);

            Assert.AreEqual(zeroEvent2, zeroEvent);
            Assert.AreEqual(second, second2);
        }
    }
}
