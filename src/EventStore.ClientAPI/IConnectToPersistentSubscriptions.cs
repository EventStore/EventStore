using System;

namespace EventStore.ClientAPI {
	internal interface IConnectToPersistentSubscriptions {
		void NotifyEventsProcessed(Guid[] processedEvents);
		void NotifyEventsFailed(Guid[] processedEvents, PersistentSubscriptionNakEventAction action, string reason);
		void Unsubscribe();
	}
}
