using System;
using EventStore.Core.Bus;
using EventStore.Projections.Core.Messages;
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
            IHandle<CoreProjectionManagementMessage.Dispose>
    {
        public void Handle(CoreProjectionManagementMessage.CreatePrepared message)
        {
            var command = new ProjectionCoreServiceCommandReader.CreatePreparedCommand
            {
                Config = new ProjectionCoreServiceCommandReader.PersistedProjectionConfig(message.Config),
                HandlerType = message.HandlerType,
                Id = message.ProjectionId.ToString("N"),
                Name = message.Name,
                Query = message.Query,
                SourceDefinition =
                    ProjectionSourceDefinition.From(
                        message.Name,
                        message.SourceDefinition,
                        message.HandlerType,
                        message.Query),
                Version = message.Version
            };
            PublishCommand("$create-prepared", message.WorkerId, command);
        }

        public void Handle(CoreProjectionManagementMessage.CreateAndPrepare message)
        {
            var command = new ProjectionCoreServiceCommandReader.CreateAndPrepareCommand
            {
                Config = new ProjectionCoreServiceCommandReader.PersistedProjectionConfig(message.Config),
                HandlerType = message.HandlerType,
                Id = message.ProjectionId.ToString("N"),
                Name = message.Name,
                Query = message.Query,
                Version = message.Version,
            };
            PublishCommand("$create-and-prepare", message.WorkerId, command);
        }

        public void Handle(CoreProjectionManagementMessage.CreateAndPrepareSlave message)
        {
            var command = new ProjectionCoreServiceCommandReader.CreateAndPrepareSlaveCommand
            {
                Config = new ProjectionCoreServiceCommandReader.PersistedProjectionConfig(message.Config),
                HandlerType = message.HandlerType,
                Id = message.ProjectionId.ToString("N"),
                Name = message.Name,
                Query = message.Query,
                Version = message.Version,
                MasterCoreProjectionId = message.MasterCoreProjectionId.ToString("N"),
                MasterWorkerId = message.MasterWorkerId.ToString("N")
            };
            PublishCommand("$create-and-prepare-slave", message.WorkerId, command);
        }

        public void Handle(ReaderSubscriptionManagement.SpoolStreamReading message)
        {
            var command = new ProjectionCoreServiceCommandReader.SpoolStreamReadingCommand
            {
                CatalogSequenceNumber = message.CatalogSequenceNumber,
                LimitingCommitPosition = message.LimitingCommitPosition,
                StreamId = message.StreamId,
                SubscriptionId = message.SubscriptionId.ToString("N")
            };
            PublishCommand("$spool-stream-reading", message.WorkerId, command);
        }

        public void Handle(CoreProjectionManagementMessage.LoadStopped message)
        {
            var command = new ProjectionCoreServiceCommandReader.LoadStoppedCommand
            {
                Id = message.ProjectionId.ToString("N")
            };
            PublishCommand("$load-stopped", message.WorkerId, command);
        }

        public void Handle(CoreProjectionManagementMessage.Start message)
        {
            var command = new ProjectionCoreServiceCommandReader.StartCommand
            {
                Id = message.ProjectionId.ToString("N")
            };
            PublishCommand("$start", message.WorkerId, command);
        }

        public void Handle(CoreProjectionManagementMessage.Stop message)
        {
            var command = new ProjectionCoreServiceCommandReader.StopCommand
            {
                Id = message.ProjectionId.ToString("N")
            };
            PublishCommand("$stop", message.WorkerId, command);
        }

        public void Handle(CoreProjectionManagementMessage.Kill message)
        {
            var command = new ProjectionCoreServiceCommandReader.KillCommand
            {
                Id = message.ProjectionId.ToString("N")
            };
            PublishCommand("$kill", message.WorkerId, command);
        }

        public void Handle(CoreProjectionManagementMessage.Dispose message)
        {
            var command = new ProjectionCoreServiceCommandReader.DisposeCommand
            {
                Id = message.ProjectionId.ToString("N")
            };
            PublishCommand("$dispose", message.WorkerId, command);
        }

        private void PublishCommand(string command, Guid workerId, object body)
        {
            throw new System.NotImplementedException();
        }

    }
}
