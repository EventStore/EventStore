using System;
using EventStore.Core.Data;

namespace EventStore.Core.Services.PersistentSubscription {
	public interface IPersistentSubscriptionStreamReader {
		void BeginReadEvents(string stream, long startEventNumber, int countToLoad, int batchSize, bool resolveLinkTos,
			Action<ResolvedEvent[], long, bool> onEventsFound);
	}
}
