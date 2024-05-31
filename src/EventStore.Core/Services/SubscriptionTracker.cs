#nullable enable
using System;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;

namespace EventStore.Core.Services;

public interface ISubscriptionTracker {
	void AddSubscription(Guid subscriptionId, string? streamName, long endOfStream);
	void RemoveSubscription(Guid subscriptionId);
	void RecordEvent(EventRecord @event);
	void ProcessEvent(ResolvedEvent @event);
}

public class SubscriptionMetricHandler(ISubscriptionTracker tracker) : IHandle<StorageMessage.EventCommitted> {
	public void Handle(StorageMessage.EventCommitted message) => tracker.RecordEvent(message.Event);
}

public class SubscriptionTracker(SubscriptionMetric metric) : ISubscriptionTracker {
	public void AddSubscription(Guid subscriptionId, string? streamName, long endOfStream) =>
		metric.Add(subscriptionId, streamName, endOfStream);

	public void RemoveSubscription(Guid subscriptionId) => metric.Remove(subscriptionId);

	public void RecordEvent(EventRecord @event) =>
		metric.RecordEvent(@event.EventStreamId, @event.TransactionPosition, @event.EventNumber);

	public void ProcessEvent(ResolvedEvent @event) => metric.ProcessEvent(@event.OriginalStreamId,
		@event.OriginalPosition?.CommitPosition ?? 0L, @event.OriginalEventNumber);

	public class NoOp : ISubscriptionTracker {
		public void AddSubscription(Guid subscriptionId, string? streamName, long endOfStream) {
		}

		public void RemoveSubscription(Guid subscriptionId) {
		}

		public void RecordEvent(EventRecord @event) {
		}

		public void ProcessEvent(ResolvedEvent @event) {
		}
	}
}
