#nullable enable
using System;

namespace EventStore.Core.Services.PersistentSubscription {
	public interface IPersistentSubscriptionCheckpointReader {
		void BeginLoadState(string subscriptionId, Action<string?> onStateLoaded);
	}
}
