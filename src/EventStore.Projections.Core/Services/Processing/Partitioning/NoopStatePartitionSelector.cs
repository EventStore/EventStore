using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing.Partitioning {
	public class NoopStatePartitionSelector : StatePartitionSelector {
		public override string GetStatePartition(EventReaderSubscriptionMessage.CommittedEventReceived @event) {
			return "";
		}

		public override bool EventReaderBasePartitionDeletedIsSupported() {
			return false;
		}
	}
}
