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
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager
{
    [TestFixture]
    public class when_requesting_partition_state_from_a_stopped_foreach_projection :
        TestFixtureWithProjectionCoreAndManagementServices
    {
        protected override void Given()
        {
            NoStream("$projections-test-projection-order");
            ExistingEvent("$projections-$all", "$ProjectionCreated", null, "test-projection");
            ExistingEvent(
                "$projections-test-projection", "$ProjectionUpdated", null,
                @"{""Query"":""fromCategory('test').foreachStream().when({'e': function(s,e){}})"", 
                    ""Mode"":""3"", ""Enabled"":false, ""HandlerType"":""JS"",
                    ""SourceDefinition"":{
                        ""AllEvents"":true,
                        ""AllStreams"":false,
                        ""Streams"":[""$ce-test""]
                    }
                }");    
            ExistingEvent("$projections-test-projection-a-checkpoint", "$Checkpoint", @"{""s"":{""$ce-test"": 9}}", @"{""data"":1}");
            NoStream("$projections-test-projection-b-checkpoint");
            ExistingEvent("$projections-test-projection-checkpoint", "$ProjectionCheckpoint", @"{""s"":{""$ce-test"": 10}}", @"{}");
            AllWritesSucceed();
        }

        private string _projectionName;

        protected override void When()
        {
            _projectionName = "test-projection";
            // when
            _manager.Handle(new SystemMessage.BecomeMaster(Guid.NewGuid()));
        }

        [Test]
        public void the_projection_state_can_be_retrieved()
        {
            _manager.Handle(new ProjectionManagementMessage.GetState(new PublishEnvelope(_bus), _projectionName, "a"));
            _manager.Handle(new ProjectionManagementMessage.GetState(new PublishEnvelope(_bus), _projectionName, "b"));

            Assert.AreEqual(2, _consumer.HandledMessages.OfType<ProjectionManagementMessage.ProjectionState>().Count());

            var first = _consumer.HandledMessages.OfType<ProjectionManagementMessage.ProjectionState>().First();
            Assert.AreEqual(_projectionName, first.Name);
            Assert.AreEqual(@"{""data"":1}", first.State);

            var second = _consumer.HandledMessages.OfType<ProjectionManagementMessage.ProjectionState>().Skip(1).First();
            Assert.AreEqual(_projectionName, second.Name);
            Assert.AreEqual("", second.State);
        }
    }
}
