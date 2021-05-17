using System;
using EventStore.Core.Data;

namespace EventStore.Core.Services.PersistentSubscription {
	public class PersistentSubscriptionSingleStreamEventSource : IPersistentSubscriptionEventSource {
		public bool FromStream => true;
		public string EventStreamId { get; }
		public bool FromAll => false;

		public PersistentSubscriptionSingleStreamEventSource(string eventStreamId) {
			EventStreamId = eventStreamId ?? throw new ArgumentNullException(nameof(eventStreamId));
		}
		public override string ToString() => EventStreamId;
		public IPersistentSubscriptionStreamPosition StreamStartPosition => new PersistentSubscriptionSingleStreamPosition(0L);
		public IPersistentSubscriptionStreamPosition GetStreamPositionFor(ResolvedEvent @event) => new PersistentSubscriptionSingleStreamPosition(@event.OriginalEventNumber);
		public IPersistentSubscriptionStreamPosition GetStreamPositionFor(string checkpoint) => new PersistentSubscriptionSingleStreamPosition(long.Parse(checkpoint));
	}
}
