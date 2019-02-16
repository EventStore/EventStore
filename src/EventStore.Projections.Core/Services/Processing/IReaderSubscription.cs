using System;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing {
	public interface IReaderSubscription : IHandle<ReaderSubscriptionMessage.CommittedEventDistributed>,
		IHandle<ReaderSubscriptionMessage.EventReaderIdle>,
		IHandle<ReaderSubscriptionMessage.EventReaderStarting>,
		IHandle<ReaderSubscriptionMessage.EventReaderEof>,
		IHandle<ReaderSubscriptionMessage.EventReaderPartitionEof>,
		IHandle<ReaderSubscriptionMessage.EventReaderPartitionDeleted>,
		IHandle<ReaderSubscriptionMessage.EventReaderPartitionMeasured>,
		IHandle<ReaderSubscriptionMessage.EventReaderNotAuthorized> {
		string Tag { get; }
		Guid SubscriptionId { get; }
		IEventReader CreatePausedEventReader(IPublisher publisher, IODispatcher ioDispatcher, Guid forkedEventReaderId);
	}
}
