using EventStore.Core.Data;
using EventStore.Core.TransactionLogV2.Hashes;
using EventStore.Core.TransactionLogV2.Services;

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
