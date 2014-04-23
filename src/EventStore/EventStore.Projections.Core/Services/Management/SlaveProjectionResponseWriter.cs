using EventStore.Core.Bus;
using EventStore.Projections.Core.Messages.ParallelQueryProcessingMessages;
using EventStore.Projections.Core.Messages.Persisted.Responses.Slave;

namespace EventStore.Projections.Core.Services.Management
{
    public sealed class SlaveProjectionResponseWriter
        : IHandle<PartitionMeasuredOutput>,
            IHandle<PartitionProcessingProgressOutput>,
            IHandle<PartitionProcessingResultOutput>
    {
        private readonly IMultiStreamMessageWriter _writer;

        public SlaveProjectionResponseWriter(IMultiStreamMessageWriter writer)
        {
            _writer = writer;
        }

        public void Handle(PartitionMeasuredOutput message)
        {
            var command = new PartitionMeasuredResponse
            {
                SubscriptionId = message.SubscriptionId.ToString("N"),
                Partition = message.Partition,
                Size = message.Size,
            };
            _writer.PublishResponse("$measured", message.MasterProjectionId, command);
        }

        public void Handle(PartitionProcessingProgressOutput message)
        {
            var command = new PartitionProcessingProgressResponse
            {
                SubscriptionId = message.SubscriptionId.ToString("N"),
                Partition = message.Partition,
                Progress = message.Progress,
            };
            _writer.PublishResponse("$progress", message.MasterProjectionId, command);
        }

        public void Handle(PartitionProcessingResultOutput message)
        {
            var command = new PartitionProcessingResultResponse
            {
                SubscriptionId = message.SubscriptionId.ToString("N"),
                Partition = message.Partition,
                CausedBy = message.CausedByGuid.ToString("N"),
                Position = message.Position,
                Result = message.Result,
            };
            _writer.PublishResponse("$result", message.MasterProjectionId, command);
        }
    }
}