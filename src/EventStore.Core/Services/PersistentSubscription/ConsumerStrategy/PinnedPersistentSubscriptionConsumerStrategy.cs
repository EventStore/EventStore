using EventStore.Core.Data;
using EventStore.Core.Index.Hashes;

namespace EventStore.Core.Services.PersistentSubscription.ConsumerStrategy {
	class PinnedPersistentSubscriptionConsumerStrategy : PinnablePersistentSubscriptionConsumerStrategy {
		public PinnedPersistentSubscriptionConsumerStrategy(IHasher streamHasher) : base(streamHasher) {
		}

		public override string Name {
			get { return SystemConsumerStrategies.Pinned; }
		}

		protected override string GetAssignmentSourceId(ResolvedEvent ev) {
			return GetSourceStreamId(ev);
		}
	}
}
