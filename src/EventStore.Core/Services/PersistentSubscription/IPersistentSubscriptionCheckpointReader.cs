using System;

namespace EventStore.Core.Services.PersistentSubscription {
	public interface IPersistentSubscriptionCheckpointReader {
		void BeginLoadState(string subscriptionId, Action<long?> onStateLoaded);
	}
}
