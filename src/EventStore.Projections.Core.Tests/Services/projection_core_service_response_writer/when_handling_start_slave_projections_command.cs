using System;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.Persisted.Responses;
using EventStore.Projections.Core.Services;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projection_core_service_response_writer
{
    [TestFixture]
    class 
        when_handling_start_slave_projections_command : specification_with_projection_core_service_response_writer
    {
        private string _name;
        private ProjectionManagementMessage.RunAs _runAs;
        private SlaveProjectionDefinitions _definition;
        private Guid _masterWorkerId;
        private Guid _masterCorrelationId;

        protected override void Given()
        {
            _name = "name";
            _runAs = ProjectionManagementMessage.RunAs.System;
            _masterCorrelationId = Guid.NewGuid();
            _masterWorkerId = Guid.NewGuid();
            _definition =
                new SlaveProjectionDefinitions(
                    new SlaveProjectionDefinitions.Definition(
                        "sl1",
                        "JS",
                        "fromAll()",
                        SlaveProjectionDefinitions.SlaveProjectionRequestedNumber.OnePerThread,
                        ProjectionMode.Transient,
                        true,
                        true,
                        true,
                        true,
                        ProjectionManagementMessage.RunAs.System));
        }

        protected override void When()
        {
            _sut.Handle(
                new ProjectionManagementMessage.Command.StartSlaveProjections(
                    new NoopEnvelope(),
                    _runAs,
                    _name,
                    _definition,
                    _masterWorkerId,
                    _masterCorrelationId));
        }

        [Test]
        public void publishes_start_slave_projections_command()
        {
            var command = AssertParsedSingleCommand<StartSlaveProjectionsCommand>("$start-slave-projections");
            Assert.AreEqual(_name, command.Name);
            Assert.AreEqual(_runAs, (ProjectionManagementMessage.RunAs)command.RunAs);
            Assert.AreEqual(_masterCorrelationId.ToString("N"), command.MasterCorrelationId);
            Assert.AreEqual(_masterWorkerId.ToString("N"), command.MasterWorkerId);
            Assert.IsNotNull(command.SlaveProjections);
            Assert.IsNotNull(command.SlaveProjections.Definitions);
            Assert.AreEqual(1, command.SlaveProjections.Definitions.Length);
            var definition = _definition.Definitions[0];
            var received = command.SlaveProjections.Definitions[0];
            Assert.AreEqual(definition.CheckpointsEnabled, received.CheckpointsEnabled);
            Assert.AreEqual(definition.EmitEnabled, received.EmitEnabled);
            Assert.AreEqual(definition.EnableRunAs, received.EnableRunAs);
            Assert.AreEqual(definition.HandlerType, received.HandlerType);
            Assert.AreEqual(definition.Mode, received.Mode);
            Assert.AreEqual(definition.Name, received.Name);
            Assert.AreEqual(definition.Query, received.Query);
            Assert.AreEqual(definition.RequestedNumber, received.RequestedNumber);
            Assert.AreEqual(definition.RunAs1.Name, received.RunAs1.Name);
        }
    }
}