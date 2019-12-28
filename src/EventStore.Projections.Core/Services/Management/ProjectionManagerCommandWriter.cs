using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.Persisted.Commands;

namespace EventStore.Projections.Core.Services.Management {
	public sealed class ProjectionManagerCommandWriter
		: IHandle<CoreProjectionManagementMessage.CreatePrepared>,
			IHandle<CoreProjectionManagementMessage.CreateAndPrepare>,
			IHandle<CoreProjectionManagementMessage.LoadStopped>,
			IHandle<CoreProjectionManagementMessage.Start>,
			IHandle<CoreProjectionManagementMessage.Stop>,
			IHandle<CoreProjectionManagementMessage.Kill>,
			IHandle<CoreProjectionManagementMessage.Dispose>,
			IHandle<CoreProjectionManagementMessage.GetState>,
			IHandle<CoreProjectionManagementMessage.GetResult>,
			IHandle<ProjectionManagementMessage.Starting> {
		private readonly IMultiStreamMessageWriter _commandWriter;

		public ProjectionManagerCommandWriter(IMultiStreamMessageWriter commandWriter) {
			_commandWriter = commandWriter;
		}

		public void Handle(ProjectionManagementMessage.Starting message) {
			_commandWriter.Reset();
		}

		public void Handle(CoreProjectionManagementMessage.CreatePrepared message) {
			var command = new CreatePreparedCommand {
				Config = new PersistedProjectionConfig(message.Config),
				HandlerType = message.HandlerType,
				Id = message.ProjectionId.ToString("N"),
				Name = message.Name,
				Query = message.Query,
				SourceDefinition = QuerySourcesDefinition.From(message.SourceDefinition),
				Version = message.Version
			};
			_commandWriter.PublishResponse("$create-prepared", message.WorkerId, command);
		}

		public void Handle(CoreProjectionManagementMessage.CreateAndPrepare message) {
			var command = new CreateAndPrepareCommand {
				Config = new PersistedProjectionConfig(message.Config),
				HandlerType = message.HandlerType,
				Id = message.ProjectionId.ToString("N"),
				Name = message.Name,
				Query = message.Query,
				Version = message.Version,
			};
			_commandWriter.PublishResponse("$create-and-prepare", message.WorkerId, command);
		}

		public void Handle(CoreProjectionManagementMessage.LoadStopped message) {
			var command = new LoadStoppedCommand {
				Id = message.ProjectionId.ToString("N")
			};
			_commandWriter.PublishResponse("$load-stopped", message.WorkerId, command);
		}

		public void Handle(CoreProjectionManagementMessage.Start message) {
			var command = new StartCommand {
				Id = message.ProjectionId.ToString("N")
			};
			_commandWriter.PublishResponse("$start", message.WorkerId, command);
		}

		public void Handle(CoreProjectionManagementMessage.Stop message) {
			var command = new StopCommand {
				Id = message.ProjectionId.ToString("N")
			};
			_commandWriter.PublishResponse("$stop", message.WorkerId, command);
		}

		public void Handle(CoreProjectionManagementMessage.Kill message) {
			var command = new KillCommand {
				Id = message.ProjectionId.ToString("N")
			};
			_commandWriter.PublishResponse("$kill", message.WorkerId, command);
		}

		public void Handle(CoreProjectionManagementMessage.Dispose message) {
			var command = new DisposeCommand {
				Id = message.ProjectionId.ToString("N")
			};
			_commandWriter.PublishResponse("$dispose", message.WorkerId, command);
		}

		public void Handle(CoreProjectionManagementMessage.GetState message) {
			var command = new GetStateCommand {
				Id = message.ProjectionId.ToString("N"),
				CorrelationId = message.CorrelationId.ToString("N"),
				Partition = message.Partition
			};
			_commandWriter.PublishResponse("$get-state", message.WorkerId, command);
		}

		public void Handle(CoreProjectionManagementMessage.GetResult message) {
			var command = new GetResultCommand {
				Id = message.ProjectionId.ToString("N"),
				CorrelationId = message.CorrelationId.ToString("N"),
				Partition = message.Partition
			};
			_commandWriter.PublishResponse("$get-result", message.WorkerId, command);
		}
	}
}
