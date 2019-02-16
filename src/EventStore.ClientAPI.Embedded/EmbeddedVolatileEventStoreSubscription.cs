using System;

namespace EventStore.ClientAPI.Embedded {
	internal class EmbeddedVolatileEventStoreSubscription : EventStoreSubscription {
		private readonly Action _unsubscribe;

		public EmbeddedVolatileEventStoreSubscription(Action unsubscribe, string streamId, long lastCommitPosition,
			long? lastEventNumber) : base(streamId, lastCommitPosition, lastEventNumber) {
			_unsubscribe = unsubscribe;
		}

		public override void Unsubscribe() {
			_unsubscribe();
		}
	}
}
