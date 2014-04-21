using EventStore.Core.Bus;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.Persisted.Commands;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Services.Management
{
    public sealed class ProjectionManagerCommandWriter
        : IHandle<CoreProjectionManagementMessage.CreatePrepared>,
            IHandle<CoreProjectionManagementMessage.CreateAndPrepare>,
            IHandle<CoreProjectionManagementMessage.CreateAndPrepareSlave>,
            IHandle<ReaderSubscriptionManagement.SpoolStreamReading>,
            IHandle<CoreProjectionManagementMessage.LoadStopped>,
            IHandle<CoreProjectionManagementMessage.Start>,
            IHandle<CoreProjectionManagementMessage.Stop>,
            IHandle<CoreProjectionManagementMessage.Kill>,
            IHandle<CoreProjectionManagementMessage.Dispose>,
            IHandle<CoreProjectionManagementMessage.GetState>,
            IHandle<CoreProjectionManagementMessage.GetResult>
    {
        private readonly ICommandWriter _commandWriter;

        public ProjectionManagerCommandWriter(ICommandWriter commandWriter)
        {
            _commandWriter = commandWriter;
        }

        public void Handle(CoreProjectionManagementMessage.CreatePrepared message)
        {
            var command = new CreatePreparedCommand
            {
                Config = new PersistedProjectionConfig(message.Config),
                HandlerType = message.HandlerType,
                Id = message.ProjectionId.ToString("N"),
                Name = message.Name,
                Query = message.Query,
                SourceDefinition = QuerySourcesDefinition.From(message.SourceDefinition),
                Version = message.Version
            };
            _commandWriter.PublishCommand("$create-prepared", message.WorkerId, command);
        }

        public void Handle(CoreProjectionManagementMessage.CreateAndPrepare message)
        {
            var command = new CreateAndPrepareCommand
            {
                Config = new PersistedProjectionConfig(message.Config),
                HandlerType = message.HandlerType,
                Id = message.ProjectionId.ToString("N"),
                Name = message.Name,
                Query = message.Query,
                Version = message.Version,
            };
            _commandWriter.PublishCommand("$create-and-prepare", message.WorkerId, command);
        }

        public void Handle(CoreProjectionManagementMessage.CreateAndPrepareSlave message)
        {
            var command = new CreateAndPrepareSlaveCommand
            {
                Config = new PersistedProjectionConfig(message.Config),
                HandlerType = message.HandlerType,
                Id = message.ProjectionId.ToString("N"),
                Name = message.Name,
                Query = message.Query,
                Version = message.Version,
                MasterCoreProjectionId = message.MasterCoreProjectionId.ToString("N"),
                MasterWorkerId = message.MasterWorkerId.ToString("N")
            };
            _commandWriter.PublishCommand("$create-and-prepare-slave", message.WorkerId, command);
        }

        public void Handle(ReaderSubscriptionManagement.SpoolStreamReading message)
        {
            var command = new SpoolStreamReadingCommand
            {
                CatalogSequenceNumber = message.CatalogSequenceNumber,
                LimitingCommitPosition = message.LimitingCommitPosition,
                StreamId = message.StreamId,
                SubscriptionId = message.SubscriptionId.ToString("N")
            };
            _commandWriter.PublishCommand("$spool-stream-reading", message.WorkerId, command);
        }

        public void Handle(CoreProjectionManagementMessage.LoadStopped message)
        {
            var command = new LoadStoppedCommand
            {
                Id = message.ProjectionId.ToString("N")
            };
            _commandWriter.PublishCommand("$load-stopped", message.WorkerId, command);
        }

        public void Handle(CoreProjectionManagementMessage.Start message)
        {
            var command = new StartCommand
            {
                Id = message.ProjectionId.ToString("N")
            };
            _commandWriter.PublishCommand("$start", message.WorkerId, command);
        }

        public void Handle(CoreProjectionManagementMessage.Stop message)
        {
            var command = new StopCommand
            {
                Id = message.ProjectionId.ToString("N")
            };
            _commandWriter.PublishCommand("$stop", message.WorkerId, command);
        }

        public void Handle(CoreProjectionManagementMessage.Kill message)
        {
            var command = new KillCommand
            {
                Id = message.ProjectionId.ToString("N")
            };
            _commandWriter.PublishCommand("$kill", message.WorkerId, command);
        }

        public void Handle(CoreProjectionManagementMessage.Dispose message)
        {
            var command = new DisposeCommand
            {
                Id = message.ProjectionId.ToString("N")
            };
            _commandWriter.PublishCommand("$dispose", message.WorkerId, command);
        }

        public void Handle(CoreProjectionManagementMessage.GetState message)
        {
            var command = new GetStateCommand
            {
                Id = message.ProjectionId.ToString("N"),
                CorrelationId = message.CorrelationId.ToString("N"),
                Partition =  message.Partition
            };
            _commandWriter.PublishCommand("$get-state", message.WorkerId, command);
        }

        public void Handle(CoreProjectionManagementMessage.GetResult message)
        {
            var command = new GetResultCommand
            {
                Id = message.ProjectionId.ToString("N"),
                CorrelationId = message.CorrelationId.ToString("N"),
                Partition = message.Partition
            };
            _commandWriter.PublishCommand("$get-result", message.WorkerId, command);
        }
    }
}
