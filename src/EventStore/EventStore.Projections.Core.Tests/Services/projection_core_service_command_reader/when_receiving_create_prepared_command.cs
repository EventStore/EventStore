using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projection_core_service_command_reader
{
    [TestFixture]
    public class when_receiving_create_prepared_command : specification_with_projection_core_service_command_reader_started
    {
        private Guid _projectionId;

        protected override IEnumerable<WhenStep> When()
        {
            _projectionId = Guid.NewGuid();
            yield return
                CreateWriteEvent(
                    "$projections-$" + _serviceId,
                    "$create-prepapred",
                    @"{
                        ""id"":""" + _projectionId.ToString("N") + @""",
                         ""config"":{
                             ""runAs"":""user"",
                             ""runAsRoles"":[""a"",""b""],
                         },
                         ""sourceDefinition"":{},
                         ""version"":{},
                         ""handlerType"":""JS"",
                         ""query"":""fromStream('$user-admin').outputState()"",
                         ""name"":""test""
                    }",
                    null,
                    true);
        }

        [Test]
        public void publishes_projection_create_prepapred_message()
        {
            var createPrepared = HandledMessages.OfType<CoreProjectionManagementMessage.CreatePrepared>().LastOrDefault();
            Assert.IsNotNull(createPrepared);
            Assert.AreEqual(_projectionId, createPrepared.ProjectionId);
        }
    }
}