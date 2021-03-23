using System;
using EventStore.Core.Data;

namespace EventStore.Core.Services.PersistentSubscription {
	public interface IPersistentSubscriptionEventSource {
		bool FromStream { get; }
		string EventStreamId { get; }
		bool FromAll { get; }
		string ToString();
		IPersistentSubscriptionStreamPosition StreamStartPosition { get; }
		IPersistentSubscriptionStreamPosition GetStreamPositionFor(ResolvedEvent @event);
		IPersistentSubscriptionStreamPosition GetStreamPositionFor(string checkpoint);
	}
}
