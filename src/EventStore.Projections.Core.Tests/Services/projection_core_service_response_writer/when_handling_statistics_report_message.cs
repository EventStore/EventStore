using System;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.Persisted.Responses;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Management;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projection_core_service_response_writer {
	[TestFixture]
	class when_handling_statistics_report_message : specification_with_projection_core_service_response_writer {
		private Guid _projectionId;
		private ProjectionStatistics _statistics;

		protected override void Given() {
			_projectionId = Guid.NewGuid();
			_statistics = new ProjectionStatistics {
				BufferedEvents = 100,
				CheckpointStatus = "checkpoint-status",
				CoreProcessingTime = 10,
				ResultStreamName = "result-stream",
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

		protected override void When() {
			_sut.Handle(new CoreProjectionStatusMessage.StatisticsReport(_projectionId, _statistics, 0));
		}

		[Test]
		public void publishes_statistics_report_response() {
			var command = AssertParsedSingleCommand<StatisticsReport>("$statistics-report");
			Assert.AreEqual(_projectionId.ToString("N"), command.Id);
			Assert.AreEqual(_statistics.BufferedEvents, command.Statistics.BufferedEvents);
			Assert.AreEqual(_statistics.CheckpointStatus, command.Statistics.CheckpointStatus);
			Assert.AreEqual(_statistics.CoreProcessingTime, command.Statistics.CoreProcessingTime);
			Assert.AreEqual(_statistics.EffectiveName, command.Statistics.EffectiveName);
			Assert.AreEqual(_statistics.Enabled, command.Statistics.Enabled);
			Assert.AreEqual(_statistics.Epoch, command.Statistics.Epoch);
			Assert.AreEqual(_statistics.EventsProcessedAfterRestart, command.Statistics.EventsProcessedAfterRestart);
			Assert.AreEqual(_statistics.LastCheckpoint, command.Statistics.LastCheckpoint);
			Assert.AreEqual(_statistics.MasterStatus, command.Statistics.MasterStatus);
			Assert.AreEqual(_statistics.Mode, command.Statistics.Mode);
			Assert.AreEqual(_statistics.Name, command.Statistics.Name);
			Assert.AreEqual(_statistics.PartitionsCached, command.Statistics.PartitionsCached);
			Assert.AreEqual(_statistics.Position, command.Statistics.Position);
			Assert.AreEqual(_statistics.Progress, command.Statistics.Progress);
			Assert.AreEqual(_statistics.ProjectionId, command.Statistics.ProjectionId);
			Assert.AreEqual(_statistics.ReadsInProgress, command.Statistics.ReadsInProgress);
			Assert.AreEqual(_statistics.StateReason, command.Statistics.StateReason);
			Assert.AreEqual(_statistics.Status, command.Statistics.Status);
			Assert.AreEqual(_statistics.Version, command.Statistics.Version);
			Assert.AreEqual(
				_statistics.WritePendingEventsAfterCheckpoint,
				command.Statistics.WritePendingEventsAfterCheckpoint);
			Assert.AreEqual(
				_statistics.WritePendingEventsBeforeCheckpoint,
				command.Statistics.WritePendingEventsBeforeCheckpoint);
			Assert.AreEqual(_statistics.WritesInProgress, command.Statistics.WritesInProgress);
			Assert.AreEqual(_statistics.ResultStreamName, command.Statistics.ResultStreamName);
		}
	}
}
