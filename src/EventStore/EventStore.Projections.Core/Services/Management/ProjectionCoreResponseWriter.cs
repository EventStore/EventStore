using EventStore.Core.Bus;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.Persisted.Responses;

namespace EventStore.Projections.Core.Services.Management
{
    public sealed class ProjectionCoreResponseWriter
        : IHandle<CoreProjectionStatusMessage.Faulted>,
            IHandle<CoreProjectionStatusMessage.Prepared>,
            IHandle<CoreProjectionManagementMessage.SlaveProjectionReaderAssigned>,
            IHandle<CoreProjectionStatusMessage.Started>,
            IHandle<CoreProjectionStatusMessage.StatisticsReport>,
            IHandle<CoreProjectionStatusMessage.Stopped>,
            IHandle<CoreProjectionStatusMessage.StateReport>,
            IHandle<CoreProjectionStatusMessage.ResultReport>
    {
        private readonly IResponseWriter _writer;

        public ProjectionCoreResponseWriter(IResponseWriter responseWriter)
        {
            _writer = responseWriter;
        }

        public void Handle(CoreProjectionStatusMessage.Faulted message)
        {
            var command = new Faulted {Id = message.ProjectionId.ToString("N"), FaultedReason = message.FaultedReason,};
            _writer.PublishCommand("$faulted", command);
        }

        public void Handle(CoreProjectionStatusMessage.Prepared message)
        {
            var command = new Prepared
            {
                Id = message.ProjectionId.ToString("N"),
                SourceDefinition = message.SourceDefinition,
            };
            _writer.PublishCommand("$prepared", command);
        }

        public void Handle(CoreProjectionManagementMessage.SlaveProjectionReaderAssigned message)
        {
            var command = new SlaveProjectionReaderAssigned
            {
                Id = message.ProjectionId.ToString("N"),
                SubscriptionId = message.SubscriptionId.ToString("N"),
            };
            _writer.PublishCommand("$slave-projection-reader-assigned", command);
        }

        public void Handle(CoreProjectionStatusMessage.Started message)
        {
            var command = new Started {Id = message.ProjectionId.ToString("N"),};
            _writer.PublishCommand("$started", command);
        }

        public void Handle(CoreProjectionStatusMessage.StatisticsReport message)
        {
            var command = new StatisticsReport
            {
                Id = message.ProjectionId.ToString("N"),
                Statistics = message.Statistics
            };
            _writer.PublishCommand("$statistics-report", command);
        }

        public void Handle(CoreProjectionStatusMessage.Stopped message)
        {
            var command = new Stopped {Id = message.ProjectionId.ToString("N"), Completed = message.Completed,};
            _writer.PublishCommand("$stopped", command);
        }

        public void Handle(CoreProjectionStatusMessage.StateReport message)
        {
            var command = new StateReport
            {
                Id = message.ProjectionId.ToString("N"),
                State = message.State,
                CorrelationId = message.CorrelationId.ToString("N"),
                Position = message.Position,
                Partition = message.Partition
            };
            _writer.PublishCommand("$state", command);
        }

        public void Handle(CoreProjectionStatusMessage.ResultReport message)
        {
            var command = new ResultReport
            {
                Id = message.ProjectionId.ToString("N"),
                Result = message.Result,
                CorrelationId = message.CorrelationId.ToString("N"),
                Position = message.Position,
                Partition = message.Partition
            };
            _writer.PublishCommand("$result", command);
        }
    }
}