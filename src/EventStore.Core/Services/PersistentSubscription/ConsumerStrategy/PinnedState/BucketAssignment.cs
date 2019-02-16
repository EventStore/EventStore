using System;

namespace EventStore.Core.Services.PersistentSubscription.ConsumerStrategy.PinnedState {
	internal struct BucketAssignment {
		internal enum BucketState {
			Unassigned,
			Assigned,
			Disconnected
		}

		public Guid NodeId { get; set; }

		public BucketState State { get; set; }

		public int InFlightCount { get; set; }

		public Node Node { get; set; }
	}
}
