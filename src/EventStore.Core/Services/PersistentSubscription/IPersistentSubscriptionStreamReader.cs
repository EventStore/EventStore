using System;
using EventStore.Core.Data;

namespace EventStore.Core.Services.PersistentSubscription {
	public interface IPersistentSubscriptionStreamReader {
		void BeginReadEvents(IPersistentSubscriptionEventSource eventSource,
			IPersistentSubscriptionStreamPosition startPosition, int countToLoad, int batchSize, int maxWindowSize,
			bool resolveLinkTos, bool skipFirstEvent,
			Action<ResolvedEvent[], IPersistentSubscriptionStreamPosition, bool> onEventsFound,
			Action<IPersistentSubscriptionStreamPosition, long> onEventsSkipped,
			Action<string> onError);
	}
}
