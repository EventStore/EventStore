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
using System.Text;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection
{
    [TestFixture]
    public class when_receiving_committed_event_the_projection_with_existing_partitioned_state_should :
        TestFixtureWithCoreProjectionStarted
    {
        private Guid _eventId;
        private string _testProjectionState = @"{""test"":1}";

        protected override void Given()
        {
            _configureBuilderByQuerySource = source =>
                {
                    source.FromAll();
                    source.AllEvents();
                    source.SetByStream();
                };
            TicksAreHandledImmediately();
            NoStream("$projections-projection-result");
            NoStream("$projections-projection-order");
            AllWritesToSucceed("$projections-projection-order");
            ExistingEvent(
                "$projections-projection-partitions", "PartitionCreated",
                @"{""c"": 100, ""p"": 50}", "account-01");
            ExistingEvent(
                "$projections-projection-account-01-result", "Result",
                @"{""c"": 100, ""p"": 50}", _testProjectionState);
            ExistingEvent(
                "$projections-projection-checkpoint", "$ProjectionCheckpoint",
                @"{""c"": 100, ""p"": 50}", _testProjectionState);
            AllWritesSucceed();
        }

        protected override void When()
        {
            //projection subscribes here
            _eventId = Guid.NewGuid();
            _consumer.HandledMessages.Clear();
            _coreProjection.Handle(
                ProjectionSubscriptionMessage.CommittedEventReceived.Sample(
                    new ResolvedEvent(
                        "account-01", 2, "account-01", 2, false, new EventPosition(120, 110), _eventId,
                        "handle_this_type", false, "data1", "metadata"), _subscriptionId, 0));
            _coreProjection.Handle(
                ProjectionSubscriptionMessage.CommittedEventReceived.Sample(
                    new ResolvedEvent(
                        "account-01", 3, "account-01", 3, false, new EventPosition(160, 150), _eventId, "append", false,
                        "$", "metadata"), 
                    _subscriptionId, 1));
        }

        [Test]
        public void register_new_partition_state_stream_only_once()
        {
            var writes =
                _writeEventHandler.HandledMessages.Where(v => v.EventStreamId == "$projections-projection-partitions")
                                  .ToArray();
            Assert.AreEqual(0, writes.Length);
        }
    }
}
