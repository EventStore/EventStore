using System;
using System.Collections.Generic;
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

        protected override IEnumerable<WhenStep> When()
        {
            _projectionName = "test-projection";
            // when
            yield return (new SystemMessage.BecomeMaster(Guid.NewGuid()));
        }

        [Test]
        public void the_projection_state_can_be_retrieved()
        {
            _manager.Handle(new ProjectionManagementMessage.GetState(new PublishEnvelope(_bus), _projectionName, "a"));
            _queue.Process();
            
            Assert.AreEqual(1, _consumer.HandledMessages.OfType<ProjectionManagementMessage.ProjectionState>().Count());

            var first = _consumer.HandledMessages.OfType<ProjectionManagementMessage.ProjectionState>().First();
            Assert.AreEqual(_projectionName, first.Name);
            Assert.AreEqual(@"{""data"":1}", first.State);

            _manager.Handle(new ProjectionManagementMessage.GetState(new PublishEnvelope(_bus), _projectionName, "b"));
            _queue.Process();

            Assert.AreEqual(2, _consumer.HandledMessages.OfType<ProjectionManagementMessage.ProjectionState>().Count());
            var second = _consumer.HandledMessages.OfType<ProjectionManagementMessage.ProjectionState>().Skip(1).First();
            Assert.AreEqual(_projectionName, second.Name);
            Assert.AreEqual("", second.State);
        }
    }
}
