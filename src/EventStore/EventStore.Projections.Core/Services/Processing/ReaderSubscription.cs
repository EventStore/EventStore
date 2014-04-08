using System;
using EventStore.Core.Bus;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class ReaderSubscription : ReaderSubscriptionBase, IReaderSubscription
    {
        public ReaderSubscription(
            string tag,
            IPublisher publisher,
            Guid subscriptionId,
            CheckpointTag @from,
            IReaderStrategy readerStrategy,
            long? checkpointUnhandledBytesThreshold,
            int? checkpointProcessedEventsThreshold,
            bool stopOnEof = false,
            int? stopAfterNEvents = null)
            : base(
                publisher,
                subscriptionId,
                @from,
                readerStrategy,
                checkpointUnhandledBytesThreshold,
                checkpointProcessedEventsThreshold,
                stopOnEof,
                stopAfterNEvents)
        {
            _tag = tag;
        }

        public void Handle(ReaderSubscriptionMessage.CommittedEventDistributed message)
        {
            ProcessOne(message);
        }

        public void Handle(ReaderSubscriptionMessage.EventReaderIdle message)
        {
            // ignore
        }

        public void Handle(ReaderSubscriptionMessage.EventReaderPartitionDeleted message)
        {
            if (!base._eventFilter.PassesDeleteNotification(message.PositionStreamId))
                return;
            var deletePosition = _positionTagger.MakeCheckpointTag(_positionTracker.LastTag, message);
            PublishPartitionDeleted(message.Partition, deletePosition);
        }
    }
}
