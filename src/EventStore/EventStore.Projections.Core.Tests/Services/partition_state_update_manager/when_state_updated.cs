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

using System.Text;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.partition_state_update_manager
{
    [TestFixture]
    public class when_state_updated
    {
        private PartitionStateUpdateManager _updateManager;
        private CheckpointTag _zero = CheckpointTag.FromPosition(0, 100, 50);
        private CheckpointTag _one = CheckpointTag.FromPosition(0, 200, 150);
        private CheckpointTag _two = CheckpointTag.FromPosition(0, 300, 250);

        [SetUp]
        public void setup()
        {
            _updateManager = new PartitionStateUpdateManager(ProjectionNamesBuilder.CreateForTest("projection"));
            _updateManager.StateUpdated("partition", new PartitionState("{\"state\":1}", null, _one), _zero);
        }

        [Test]
        public void handles_state_updated_for_the_same_partition()
        {
            _updateManager.StateUpdated("partition", new PartitionState("{\"state\":1}", null, _two), _one);
        }

        [Test]
        public void handles_state_updated_for_another_partition()
        {
            _updateManager.StateUpdated("partition", new PartitionState("{\"state\":1}", null, _two), _one);
        }

        [Test]
        public void emit_events_writes_single_state_updated_event()
        {
            var eventWriter = new FakeEventWriter();
            _updateManager.EmitEvents(eventWriter);
            Assert.AreEqual(1, eventWriter.Writes.Count);
            Assert.AreEqual(1, eventWriter.Writes[0].Length);
        }

        [Test]
        public void emit_events_writes_correct_state_data()
        {
            var eventWriter = new FakeEventWriter();
            _updateManager.EmitEvents(eventWriter);
            EmittedEvent @event = eventWriter.Writes[0][0];
            Assert.AreEqual("[{\"state\":1}]", @event.Data);
        }

        [Test]
        public void emit_events_writes_event_with_correct_caused_by_tag()
        {
            var eventWriter = new FakeEventWriter();
            _updateManager.EmitEvents(eventWriter);
            EmittedEvent @event = eventWriter.Writes[0][0];
            Assert.AreEqual(_one, @event.CausedByTag);
        }

        [Test]
        public void emit_events_writes_event_with_correct_expected_tag()
        {
            var eventWriter = new FakeEventWriter();
            _updateManager.EmitEvents(eventWriter);
            EmittedEvent @event = eventWriter.Writes[0][0];
            Assert.AreEqual(_zero, @event.ExpectedTag);
        }

    }
}
