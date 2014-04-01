using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projection_core_service_command_reader
{
    [TestFixture]
    public class when_receiving_create_and_prepare_slave_command : specification_with_projection_core_service_command_reader_started
    {
        private const string Query = @"fromStream('$user-admin').outputState()";
        private Guid _projectionId;

        protected override IEnumerable<WhenStep> When()
        {
            _projectionId = Guid.NewGuid();
            yield return
                CreateWriteEvent(
                    "$projections-$" + _serviceId,
                    "$create-and-prepare-slave",
                    @"{
                        ""id"":""" + _projectionId.ToString("N") + @""",
                          ""config"":{
                             ""runAs"":""user"",
                             ""runAsRoles"":[""a"",""b""],
                             ""checkpointHandledThreshold"":1000,
                             ""checkpointUnhandledBytesThreshold"":10000,
                             ""pendingEventsThreshold"":5000, 
                             ""maxWriteBatchLength"":100,
                             ""emitEventEnabled"":true,
                             ""checkpointsEnabled"":true,
                             ""createTempStreams"":true,
                             ""stopOnEof"":true,
                             ""isSlaveProjection"":true,
                         },
                         ""version"":{},
                         ""handlerType"":""JS"",
                         ""query"":""" + Query + @""",
                         ""name"":""test"",
                         ""masterWorkerId"":""2251f64ac0a24c599414d7fe2ada13b1"",
                         ""masterCoreProjectionId"":""85032c4bc58546b4be116f5c4b305bb6"",
                    }",
                    null,
                    true);
        }

        [Test]
        public void publishes_projection_create_prepapre_slave_message()
        {
            var createPrepare =
                HandledMessages.OfType<CoreProjectionManagementMessage.CreateAndPrepareSlave>().LastOrDefault();
            Assert.IsNotNull(createPrepare);
            Assert.AreEqual(_projectionId, createPrepare.ProjectionId);
            Assert.AreEqual("JS", createPrepare.HandlerType);
            Assert.AreEqual(Query, createPrepare.Query);
            Assert.AreEqual("test", createPrepare.Name);
            Assert.IsNotNull(createPrepare.Config);
            Assert.AreEqual("user", createPrepare.Config.RunAs.Identity.Name);
            Assert.That(createPrepare.Config.RunAs.IsInRole("b"));
            Assert.AreEqual(1000, createPrepare.Config.CheckpointHandledThreshold);
            Assert.AreEqual(10000, createPrepare.Config.CheckpointUnhandledBytesThreshold);
            Assert.AreEqual(5000, createPrepare.Config.PendingEventsThreshold);
            Assert.AreEqual(100, createPrepare.Config.MaxWriteBatchLength);
            Assert.AreEqual(true, createPrepare.Config.EmitEventEnabled);
            Assert.AreEqual(true, createPrepare.Config.CheckpointsEnabled);
            Assert.AreEqual(true, createPrepare.Config.CreateTempStreams);
            Assert.AreEqual(true, createPrepare.Config.StopOnEof);
            Assert.AreEqual(true, createPrepare.Config.IsSlaveProjection);
        }
    }
}