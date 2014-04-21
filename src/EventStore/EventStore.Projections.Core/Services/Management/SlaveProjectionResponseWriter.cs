using EventStore.Core.Bus;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.ParallelQueryProcessingMessages;
using EventStore.Projections.Core.Messages.Persisted.Commands;
using EventStore.Projections.Core.Messages.Persisted.Responses.Slave;

namespace EventStore.Projections.Core.Services.Management
{
    public sealed class SlaveProjectionResponseWriter
        : IHandle<PartitionMeasured>,
            IHandle<PartitionProcessingProgress>,
            IHandle<PartitionProcessingResult>
    {
        private readonly IMultiStreamMessageWriter _writer;

        public SlaveProjectionResponseWriter(IMultiStreamMessageWriter writer)
        {
            _writer = writer;
        }

        public void Handle(PartitionMeasured message)
        {
            var command = new PartitionMeasuredResponse
            {
                Partition = message.Partition,
                Size = message.Size,
            };
            _writer.PublishResponse("$measured", message.SubscriptionId, command);
        }

        public void Handle(PartitionProcessingProgress message)
        {
            var command = new PartitionProcessingProgressResponse
            {
                Partition = message.Partition,
                Progress = message.Progress,
            };
            _writer.PublishResponse("$progress", message.SubscriptionId, command);
        }

        public void Handle(PartitionProcessingResult message)
        {
            var command = new PartitionProcessingResultResponse
            {
                Partition = message.Partition,
                CausedBy = message.CausedByGuid.ToString("N"),
                Position = message.Position,
                Result = message.Result,
            };
            _writer.PublishResponse("$result", message.SubscriptionId, command);
        }
    }
}