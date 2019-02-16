using System;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;

namespace EventStore.Projections.Core.Services.Processing {
	public interface IReaderStrategy {
		bool IsReadingOrderRepeatable { get; }
		EventFilter EventFilter { get; }
		PositionTagger PositionTagger { get; }

		IReaderSubscription CreateReaderSubscription(
			IPublisher publisher, CheckpointTag fromCheckpointTag, Guid subscriptionId,
			ReaderSubscriptionOptions readerSubscriptionOptions);

		IEventReader CreatePausedEventReader(
			Guid eventReaderId, IPublisher publisher, IODispatcher ioDispatcher, CheckpointTag checkpointTag,
			bool stopOnEof, int? stopAfterNEvents);
	}
}
