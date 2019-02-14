using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.Persisted.Responses;

namespace EventStore.Projections.Core.Services.Management {
	public sealed class ProjectionCoreResponseWriter
		: IHandle<CoreProjectionStatusMessage.Faulted>,
			IHandle<CoreProjectionStatusMessage.Prepared>,
			IHandle<CoreProjectionManagementMessage.SlaveProjectionReaderAssigned>,
			IHandle<CoreProjectionStatusMessage.Started>,
			IHandle<CoreProjectionStatusMessage.StatisticsReport>,
			IHandle<CoreProjectionStatusMessage.Stopped>,
			IHandle<CoreProjectionStatusMessage.StateReport>,
			IHandle<CoreProjectionStatusMessage.ResultReport>,
			IHandle<ProjectionManagementMessage.Command.Abort>,
			IHandle<ProjectionManagementMessage.Command.Disable>,
			IHandle<ProjectionManagementMessage.Command.Enable>,
			IHandle<ProjectionManagementMessage.Command.GetQuery>,
			IHandle<ProjectionManagementMessage.Command.GetResult>,
			IHandle<ProjectionManagementMessage.Command.GetState>,
			IHandle<ProjectionManagementMessage.Command.GetStatistics>,
			IHandle<ProjectionManagementMessage.Command.Post>,
			IHandle<ProjectionManagementMessage.Command.Reset>,
			IHandle<ProjectionManagementMessage.Command.SetRunAs>,
			IHandle<ProjectionManagementMessage.Command.StartSlaveProjections>,
			IHandle<ProjectionManagementMessage.Command.UpdateQuery>,
			IHandle<ProjectionManagementMessage.Command.Delete>,
			IHandle<ProjectionCoreServiceMessage.StartCore> {
		private readonly IResponseWriter _writer;

		public ProjectionCoreResponseWriter(IResponseWriter responseWriter) {
			_writer = responseWriter;
		}

		public void Handle(ProjectionCoreServiceMessage.StartCore message) {
			_writer.Reset();
		}

		public void Handle(CoreProjectionStatusMessage.Faulted message) {
			var command = new Faulted {Id = message.ProjectionId.ToString("N"), FaultedReason = message.FaultedReason,};
			_writer.PublishCommand("$faulted", command);
		}

		public void Handle(CoreProjectionStatusMessage.Prepared message) {
			var command = new Prepared {
				Id = message.ProjectionId.ToString("N"),
				SourceDefinition = message.SourceDefinition,
			};
			_writer.PublishCommand("$prepared", command);
		}

		public void Handle(CoreProjectionManagementMessage.SlaveProjectionReaderAssigned message) {
			var command = new SlaveProjectionReaderAssigned {
				Id = message.ProjectionId.ToString("N"),
				SubscriptionId = message.SubscriptionId.ToString("N"),
			};
			_writer.PublishCommand("$slave-projection-reader-assigned", command);
		}

		public void Handle(CoreProjectionStatusMessage.Started message) {
			var command = new Started {Id = message.ProjectionId.ToString("N"),};
			_writer.PublishCommand("$started", command);
		}

		public void Handle(CoreProjectionStatusMessage.StatisticsReport message) {
			var command = new StatisticsReport {
				Id = message.ProjectionId.ToString("N"),
				Statistics = message.Statistics
			};
			_writer.PublishCommand("$statistics-report", command);
		}

		public void Handle(CoreProjectionStatusMessage.Stopped message) {
			var command = new Stopped
				{Id = message.ProjectionId.ToString("N"), Completed = message.Completed, Name = message.Name};
			_writer.PublishCommand("$stopped", command);
		}

		public void Handle(CoreProjectionStatusMessage.StateReport message) {
			var command = new StateReport {
				Id = message.ProjectionId.ToString("N"),
				State = message.State,
				CorrelationId = message.CorrelationId.ToString("N"),
				Position = message.Position,
				Partition = message.Partition
			};
			_writer.PublishCommand("$state", command);
		}

		public void Handle(CoreProjectionStatusMessage.ResultReport message) {
			var command = new ResultReport {
				Id = message.ProjectionId.ToString("N"),
				Result = message.Result,
				CorrelationId = message.CorrelationId.ToString("N"),
				Position = message.Position,
				Partition = message.Partition
			};
			_writer.PublishCommand("$result", command);
		}

		public void Handle(ProjectionManagementMessage.Command.Abort message) {
			var command = new AbortCommand {
				Name = message.Name,
				RunAs = message.RunAs,
			};
			_writer.PublishCommand("$abort", command);
		}

		public void Handle(ProjectionManagementMessage.Command.Disable message) {
			var command = new DisableCommand {
				Name = message.Name,
				RunAs = message.RunAs,
			};
			_writer.PublishCommand("$disable", command);
		}

		public void Handle(ProjectionManagementMessage.Command.Enable message) {
			var command = new EnableCommand {
				Name = message.Name,
				RunAs = message.RunAs,
			};
			_writer.PublishCommand("$enable", command);
		}

		public void Handle(ProjectionManagementMessage.Command.GetQuery message) {
			var command = new GetQueryCommand {
				Name = message.Name,
				RunAs = message.RunAs,
			};
			_writer.PublishCommand("$get-query", command);
		}

		public void Handle(ProjectionManagementMessage.Command.GetResult message) {
			var command = new GetResultCommand {
				Name = message.Name,
				Partition = message.Partition,
			};
			_writer.PublishCommand("$get-result", command);
		}

		public void Handle(ProjectionManagementMessage.Command.GetState message) {
			var command = new GetStateCommand {
				Name = message.Name,
				Partition = message.Partition,
			};
			_writer.PublishCommand("$get-state", command);
		}

		public void Handle(ProjectionManagementMessage.Command.GetStatistics message) {
			var command = new GetStatisticsCommand {
				Name = message.Name,
				IncludeDeleted = message.IncludeDeleted,
				Mode = message.Mode
			};
			_writer.PublishCommand("$get-statistics", command);
		}

		public void Handle(ProjectionManagementMessage.Command.Post message) {
			var command = new PostCommand {
				Name = message.Name,
				RunAs = message.RunAs,
				CheckpointsEnabled = message.CheckpointsEnabled,
				TrackEmittedStreams = message.TrackEmittedStreams,
				EmitEnabled = message.EmitEnabled,
				EnableRunAs = message.EnableRunAs,
				Enabled = message.Enabled,
				HandlerType = message.HandlerType,
				Mode = message.Mode,
				Query = message.Query,
			};
			_writer.PublishCommand("$post", command);
		}

		public void Handle(ProjectionManagementMessage.Command.Reset message) {
			var command = new ResetCommand {
				Name = message.Name,
				RunAs = message.RunAs,
			};
			_writer.PublishCommand("$reset", command);
		}

		public void Handle(ProjectionManagementMessage.Command.SetRunAs message) {
			var command = new SetRunAsCommand {
				Name = message.Name,
				RunAs = message.RunAs,
				SetRemove = message.Action,
			};
			_writer.PublishCommand("$set-runas", command);
		}

		public void Handle(ProjectionManagementMessage.Command.StartSlaveProjections message) {
			var command = new StartSlaveProjectionsCommand {
				Name = message.Name,
				RunAs = message.RunAs,
				MasterCorrelationId = message.MasterCorrelationId.ToString("N"),
				MasterWorkerId = message.MasterWorkerId.ToString("N"),
				SlaveProjections = message.SlaveProjections,
			};
			_writer.PublishCommand("$start-slave-projections", command);
		}

		public void Handle(ProjectionManagementMessage.Command.UpdateQuery message) {
			var command = new UpdateQueryCommand {
				Name = message.Name,
				RunAs = message.RunAs,
				EmitEnabled = message.EmitEnabled,
				HandlerType = message.HandlerType,
				Query = message.Query,
			};
			_writer.PublishCommand("$update-query", command);
		}

		public void Handle(ProjectionManagementMessage.Command.Delete message) {
			var command = new DeleteCommand {
				Name = message.Name,
				RunAs = message.RunAs,
				DeleteCheckpointStream = message.DeleteCheckpointStream,
				DeleteStateStream = message.DeleteStateStream,
				DeleteEmittedStreams = message.DeleteEmittedStreams,
			};
			_writer.PublishCommand("$delete", command);
		}
	}
}
