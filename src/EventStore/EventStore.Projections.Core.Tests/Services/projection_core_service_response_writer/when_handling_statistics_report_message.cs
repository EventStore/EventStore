using System;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Management;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projection_core_service_response_writer
{
    [TestFixture]
    class when_handling_statistics_report_message : specification_with_projection_core_service_response_writer
    {
        private Guid _projectionId;
        private ProjectionStatistics _statistics;
        private ProjectionSourceDefinition _definition;

        protected override void Given()
        {
            _projectionId = Guid.NewGuid();
            var builder = new SourceDefinitionBuilder();
            builder.FromStream("s1");
            builder.FromStream("s2");
            builder.IncludeEvent("e1");
            builder.IncludeEvent("e2");
            builder.SetByStream();
            builder.SetResultStreamNameOption("result-stream");
            _definition = ProjectionSourceDefinition.From("name", builder, "JS", "query");
            _statistics = new ProjectionStatistics
            {
                BufferedEvents = 100,
                CheckpointStatus = "checkpoint-status",
                CoreProcessingTime = 10,
                Definition = _definition,
                EffectiveName = "effective-name",
                Enabled = true,
                Epoch = 10,
                EventsProcessedAfterRestart = 12345,
                LastCheckpoint = "last-chgeckpoint",
                MasterStatus = ManagedProjectionState.Completed,
                Mode = ProjectionMode.OneTime,
                PartitionsCached = 123,
                Name = "name",
                Position = CheckpointTag.FromPosition(0, 1000, 900).ToString(),
                Progress = 100,
                ProjectionId = 1234,
                ReadsInProgress = 2,
                StateReason = "reason",
                Status = "status",
                Version = 1,
                WritePendingEventsAfterCheckpoint = 3,
                WritePendingEventsBeforeCheckpoint = 4,
                WritesInProgress = 5,
                
                
            };
        }

        protected override void When()
        {
            _sut.Handle(new CoreProjectionManagementMessage.StatisticsReport(_projectionId, _statistics));
        }

        [Test]
        public void publishes_statistics_report_response()
        {
            var command = AssertParsedSingleCommand<ProjectionCoreResponseWriter.StatisticsReport>("$statistics-report");
            Assert.AreEqual(_projectionId.ToString("N"), command.Id);
            Assert.AreEqual(_statistics.BufferedEvents, command.Statistcs.BufferedEvents);
            Assert.AreEqual(_statistics.CheckpointStatus, command.Statistcs.CheckpointStatus);
            Assert.AreEqual(_statistics.CoreProcessingTime, command.Statistcs.CoreProcessingTime);
            Assert.AreEqual(_statistics.EffectiveName, command.Statistcs.EffectiveName);
            Assert.AreEqual(_statistics.Enabled, command.Statistcs.Enabled);
            Assert.AreEqual(_statistics.Epoch, command.Statistcs.Epoch);
            Assert.AreEqual(_statistics.EventsProcessedAfterRestart, command.Statistcs.EventsProcessedAfterRestart);
            Assert.AreEqual(_statistics.LastCheckpoint, command.Statistcs.LastCheckpoint);
            Assert.AreEqual(_statistics.MasterStatus, command.Statistcs.MasterStatus);
            Assert.AreEqual(_statistics.Mode, command.Statistcs.Mode);
            Assert.AreEqual(_statistics.Name, command.Statistcs.Name);
            Assert.AreEqual(_statistics.PartitionsCached, command.Statistcs.PartitionsCached);
            Assert.AreEqual(_statistics.Position, command.Statistcs.Position);
            Assert.AreEqual(_statistics.Progress, command.Statistcs.Progress);
            Assert.AreEqual(_statistics.ProjectionId, command.Statistcs.ProjectionId);
            Assert.AreEqual(_statistics.ReadsInProgress, command.Statistcs.ReadsInProgress);
            Assert.AreEqual(_statistics.StateReason, command.Statistcs.StateReason);
            Assert.AreEqual(_statistics.Status, command.Statistcs.Status);
            Assert.AreEqual(_statistics.Version, command.Statistcs.Version);
            Assert.AreEqual(
                _statistics.WritePendingEventsAfterCheckpoint,
                command.Statistcs.WritePendingEventsAfterCheckpoint);
            Assert.AreEqual(
                _statistics.WritePendingEventsBeforeCheckpoint,
                command.Statistcs.WritePendingEventsBeforeCheckpoint);
            Assert.AreEqual(_statistics.WritesInProgress, command.Statistcs.WritesInProgress);
            Assert.AreEqual(_statistics.Definition, command.Statistcs.Definition);
        }
    }
}