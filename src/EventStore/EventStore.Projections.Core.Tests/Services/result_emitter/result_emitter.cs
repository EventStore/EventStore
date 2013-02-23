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
using System.Text;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.result_emitter
{
    public static class result_emitter
    {

        [TestFixture]
        public class when_creating
        {
            private ProjectionNamesBuilder _namesBuilder;

            [SetUp]
            public void setup()
            {
                _namesBuilder = new ProjectionNamesBuilder("projection");
            }

            [Test]
            public void it_can_be_created()
            {
                var re = new ResultEmitter(_namesBuilder);
            }

            [Test, ExpectedException(typeof (ArgumentNullException))]
            public void null_names_builder_throws_argument_null_exception()
            {
                var re = new ResultEmitter(null);
            }
        }

        [TestFixture]
        public class when_new_partition
        {
            private ProjectionNamesBuilder _namesBuilder;
            private ResultEmitter _re;
            private string _partition;
            private string _projection;
            private CheckpointTag _partitionAt;
            private EmittedEvent[] _emittedEvents;

            [SetUp]
            public void setup()
            {
                Given();
                When();
            }

            private void Given()
            {
                _projection = "projection";
                _partitionAt = CheckpointTag.FromPosition(100, 50);
                _partition = "partition";
                _namesBuilder = new ProjectionNamesBuilder(_projection);
                _re = new ResultEmitter(_namesBuilder);
            }

            private void When()
            {
                _emittedEvents = _re.NewPartition(_partition, _partitionAt);
            }

            [Test]
            public void emits_no_events()
            {
                Assert.IsTrue(_emittedEvents == null || _emittedEvents.Length == 0);
            }


        }

        [TestFixture]
        public class when_result_updated
        {
            private ProjectionNamesBuilder _namesBuilder;
            private ResultEmitter _re;
            private string _partition;
            private string _projection;
            private CheckpointTag _resultAt;
            private EmittedEvent[] _emittedEvents;
            private string _result;

            [SetUp]
            public void setup()
            {
                Given();
                When();
            }

            private void Given()
            {
                _projection = "projection";
                _resultAt = CheckpointTag.FromPosition(100, 50);
                _partition = "partition";
                _result = "{\"result\":1}";
                _namesBuilder = new ProjectionNamesBuilder(_projection);
                _re = new ResultEmitter(_namesBuilder);
            }

            private void When()
            {
                _emittedEvents = _re.ResultUpdated(_partition, _result, _resultAt);
            }

            [Test]
            public void emits_result_event()
            {
                Assert.NotNull(_emittedEvents);
                Assert.AreEqual(2, _emittedEvents.Length);
                var @event = _emittedEvents[0];
                var link = _emittedEvents[1];

                Assert.AreEqual("Result", @event.EventType);
                Assert.AreEqual(_result, Encoding.UTF8.GetString(@event.Data));
                Assert.AreEqual("$projections-projection-partition-result", @event.StreamId);
                Assert.AreEqual(_resultAt, @event.CausedByTag);
                Assert.IsNull(@event.ExpectedTag);

                Assert.AreEqual("$>", link.EventType);
                ((EmittedLinkTo) link).SetTargetEventNumber(1);
                Assert.AreEqual("1@$projections-projection-partition-result", Encoding.UTF8.GetString(link.Data));
                Assert.AreEqual("$projections-projection-result", link.StreamId);
                Assert.AreEqual(_resultAt, link.CausedByTag);
                Assert.IsNull(link.ExpectedTag);

            }


        }

        [TestFixture]
        public class when_result_removed
        {
            private ProjectionNamesBuilder _namesBuilder;
            private ResultEmitter _re;
            private string _partition;
            private string _projection;
            private CheckpointTag _resultAt;
            private EmittedEvent[] _emittedEvents;

            [SetUp]
            public void setup()
            {
                Given();
                When();
            }

            private void Given()
            {
                _projection = "projection";
                _resultAt = CheckpointTag.FromPosition(100, 50);
                _partition = "partition";
                _namesBuilder = new ProjectionNamesBuilder(_projection);
                _re = new ResultEmitter(_namesBuilder);
            }

            private void When()
            {
                _emittedEvents = _re.ResultUpdated(_partition, null, _resultAt);
            }

            [Test]
            public void emits_result_event()
            {
                Assert.NotNull(_emittedEvents);
                Assert.AreEqual(2, _emittedEvents.Length);
                var @event = _emittedEvents[0];
                var link = _emittedEvents[1];

                Assert.AreEqual("ResultRemoved", @event.EventType);
                Assert.IsNull(@event.Data);
                Assert.AreEqual("$projections-projection-partition-result", @event.StreamId);
                Assert.AreEqual(_resultAt, @event.CausedByTag);
                Assert.IsNull(@event.ExpectedTag);

                Assert.AreEqual("$>", link.EventType);
                ((EmittedLinkTo)link).SetTargetEventNumber(1);
                Assert.AreEqual("1@$projections-projection-partition-result", Encoding.UTF8.GetString(link.Data));
                Assert.AreEqual("$projections-projection-result", link.StreamId);
                Assert.AreEqual(_resultAt, link.CausedByTag);
                Assert.IsNull(link.ExpectedTag);


            }


        }

    }
}
