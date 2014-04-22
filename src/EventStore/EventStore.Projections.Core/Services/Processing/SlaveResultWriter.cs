using System;
using EventStore.Core.Bus;
using EventStore.Projections.Core.Messages.ParallelQueryProcessingMessages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class SlaveResultWriter : IResultWriter
    {
        private readonly Guid _workerId;
        private readonly Guid _masterCoreProjectionId;
        private readonly IPublisher _resultsPublisher;

        public SlaveResultWriter(Guid workerId, IPublisher publisher, Guid masterCoreProjectionId)
        {
            _resultsPublisher = publisher;
            _workerId = workerId;
            _masterCoreProjectionId = masterCoreProjectionId;
        }

        public void WriteEofResult(
            Guid subscriptionId, string partition, string resultBody, CheckpointTag causedBy, Guid causedByGuid,
            string correlationId)
        {
            _resultsPublisher.Publish(
                new PartitionProcessingResult(
                    _workerId,
                    _masterCoreProjectionId,
                    subscriptionId,
                    partition,
                    causedByGuid,
                    causedBy,
                    resultBody));
        }

        public void WritePartitionMeasured(Guid subscriptionId, string partition, int size)
        {
            _resultsPublisher.Publish(
                new PartitionMeasured(_workerId, _masterCoreProjectionId, subscriptionId, partition, size));
        }

        public void WriteRunningResult(EventProcessedResult result)
        {
            // intentionally does nothing            
        }

        public void AccountPartition(EventProcessedResult result)
        {
            // intentionally does nothing            
        }

        public void EventsEmitted(
            EmittedEventEnvelope[] scheduledWrites, Guid causedBy, string correlationId)
        {
            throw new NotSupportedException();
        }

        public void WriteProgress(Guid subscriptionId, float progress)
        {
            _resultsPublisher.Publish(
                new PartitionProcessingProgress(_workerId, _masterCoreProjectionId, subscriptionId, progress));
        }
    }
}
