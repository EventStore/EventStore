using System;
using EventStore.Core.Bus;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.ParallelQueryProcessingMessages;

namespace EventStore.Projections.Core.Services {
	public sealed class ReaderSubscriptionDispatcher :
		PublishSubscribeDispatcher
		<Guid, ReaderSubscriptionManagement.Subscribe,
			ReaderSubscriptionManagement.ReaderSubscriptionManagementMessage, EventReaderSubscriptionMessageBase> {
		public ReaderSubscriptionDispatcher(IPublisher publisher)
			: base(publisher, v => v.SubscriptionId, v => v.SubscriptionId) {
		}
	}

	public sealed class SpooledStreamReadingDispatcher :
		PublishSubscribeDispatcher
		<Tuple<Guid, string>, ReaderSubscriptionManagement.SpoolStreamReading,
			ReaderSubscriptionManagement.SpoolStreamReading, PartitionProcessingResultBase> {
		public SpooledStreamReadingDispatcher(IPublisher publisher)
			: base(
				publisher, reading => Tuple.Create(reading.SubscriptionId, reading.StreamId),
				reading => Tuple.Create(reading.SubscriptionId, reading.Partition)) {
		}
	}
}
