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
    class when_handling_create_prepared_message : specification_with_projection_manager_command_writer
    {
        private Guid _projectionId;
        private Guid _workerId;
        private ProjectionConfig _config;
        private QuerySourcesDefinition _definition;
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

            var builder = new SourceDefinitionBuilder();
            builder.FromStream("s1");
            builder.FromStream("s2");
            builder.IncludeEvent("e1");
            builder.IncludeEvent("e2");
            builder.SetByStream();
            builder.SetResultStreamNameOption("result-stream");
            _definition = QuerySourcesDefinition.From(builder);
        }

        protected override void When()
        {
            _projectionVersion = new ProjectionVersion(1, 2, 3);
            _sut.Handle(
                new CoreProjectionManagementMessage.CreatePrepared(
                    _projectionId,
                    _workerId,
                    _projectionName,
                    _projectionVersion,
                    _config,
                    _definition,
                    _handlerType,
                    _query));
        }

        [Test]
        public void publishes_create_prepared_command()
        {
            var command =
                AssertParsedSingleCommand<CreatePreparedCommand>(
                    "$create-prepared",
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
            Assert.AreEqual(_definition.AllEvents, command.SourceDefinition.AllEvents);
            Assert.AreEqual(_definition.AllStreams, command.SourceDefinition.AllStreams);
            Assert.AreEqual(_definition.ByCustomPartitions, command.SourceDefinition.ByCustomPartitions);
            Assert.AreEqual(_definition.ByStreams, command.SourceDefinition.ByStreams);
            Assert.AreEqual(_definition.CatalogStream, command.SourceDefinition.CatalogStream);
            Assert.AreEqual(_definition.Categories, command.SourceDefinition.Categories);
            Assert.AreEqual(_definition.Events, command.SourceDefinition.Events);
            Assert.AreEqual(_definition.LimitingCommitPosition, command.SourceDefinition.LimitingCommitPosition);
            Assert.AreEqual(_definition.Streams, command.SourceDefinition.Streams);
            Assert.AreEqual(
                _definition.Options.DefinesCatalogTransform,
                command.SourceDefinition.Options.DefinesCatalogTransform);
            Assert.AreEqual(_definition.Options.DefinesFold, command.SourceDefinition.Options.DefinesFold);
            Assert.AreEqual(
                _definition.Options.DefinesStateTransform,
                command.SourceDefinition.Options.DefinesStateTransform);
            Assert.AreEqual(_definition.Options.DisableParallelism, command.SourceDefinition.Options.DisableParallelism);
            Assert.AreEqual(
                _definition.Options.ForceProjectionName,
                command.SourceDefinition.Options.ForceProjectionName);
            Assert.AreEqual(
                _definition.Options.HandlesDeletedNotifications,
                command.SourceDefinition.Options.HandlesDeletedNotifications);
            Assert.AreEqual(_definition.Options.IncludeLinks, command.SourceDefinition.Options.IncludeLinks);
            Assert.AreEqual(_definition.Options.IsBiState, command.SourceDefinition.Options.IsBiState);
            Assert.AreEqual(
                _definition.Options.PartitionResultStreamNamePattern,
                command.SourceDefinition.Options.PartitionResultStreamNamePattern);
            Assert.AreEqual(_definition.Options.ProcessingLag, command.SourceDefinition.Options.ProcessingLag);
            Assert.AreEqual(_definition.Options.ProducesResults, command.SourceDefinition.Options.ProducesResults);
            Assert.AreEqual(_definition.Options.ReorderEvents, command.SourceDefinition.Options.ReorderEvents);
            Assert.AreEqual(_definition.Options.ResultStreamName, command.SourceDefinition.Options.ResultStreamName);

        }
    }
}