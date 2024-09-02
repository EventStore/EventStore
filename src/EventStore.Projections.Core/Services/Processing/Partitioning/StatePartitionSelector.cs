using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing.Partitioning {
	public abstract class StatePartitionSelector {
		public abstract string GetStatePartition(EventReaderSubscriptionMessage.CommittedEventReceived @event);
		public abstract bool EventReaderBasePartitionDeletedIsSupported();
	}
}
