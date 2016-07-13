using System;
using EventStore.Core.Authentication;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.Persisted.Commands;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager.command_writer
{
    [TestFixture]
    class when_handling_create_and_prepare_message : specification_with_projection_manager_command_writer
    {
        private Guid _projectionId;
        private Guid _workerId;
        private ProjectionConfig _config;
        private ProjectionVersion _projectionVersion;
        private string _projectionName;
        private string _handlerType;
        private string _query;

        protected override void Given()
        {
            _projectionId = Guid.NewGuid();
            _workerId = Guid.NewGuid();
            _projectionName = "projection";
            _handlerType = "JS";
            _query = "from()";

            _config = new ProjectionConfig(
                new OpenGenericPrincipal("user", "a", "b"),
                1000,
                100000,
                2000,
                200,
                true,
                true,
                true,
                true,
                true,
                true);

        }

        protected override void When()
        {
            _projectionVersion = new ProjectionVersion(1, 2, 3);
            _sut.Handle(
                new CoreProjectionManagementMessage.CreateAndPrepare(
                    _projectionId,
                    _workerId,
                    _projectionName,
                    _projectionVersion,
                    _config,
                    _handlerType,
                    _query));
        }

        [Test]
        public void publishes_create_and_prepare_command()
        {
            var command =
                AssertParsedSingleCommand<CreateAndPrepareCommand>(
                    "$create-and-prepare",
                    _workerId);
            Assert.AreEqual(_projectionId.ToString("N"), command.Id);
            Assert.AreEqual(_handlerType, command.HandlerType);
            Assert.AreEqual(_projectionName, command.Name);
            Assert.AreEqual(_query, command.Query);
            Assert.AreEqual(
                (PersistedProjectionVersion) _projectionVersion,
                command.Version);
            Assert.AreEqual(_config.CheckpointHandledThreshold, command.Config.CheckpointHandledThreshold);
            Assert.AreEqual(_config.CheckpointUnhandledBytesThreshold, command.Config.CheckpointUnhandledBytesThreshold);
            Assert.AreEqual(_config.CheckpointsEnabled, command.Config.CheckpointsEnabled);
            Assert.AreEqual(_config.CreateTempStreams, command.Config.CreateTempStreams);
            Assert.AreEqual(_config.EmitEventEnabled, command.Config.EmitEventEnabled);
            Assert.AreEqual(_config.IsSlaveProjection, command.Config.IsSlaveProjection);
            Assert.AreEqual(_config.MaxWriteBatchLength, command.Config.MaxWriteBatchLength);
            Assert.AreEqual(_config.PendingEventsThreshold, command.Config.PendingEventsThreshold);
            Assert.AreEqual(_config.RunAs.Identity.Name, command.Config.RunAs);
            Assert.AreEqual(_config.StopOnEof, command.Config.StopOnEof);
        }
    }
}