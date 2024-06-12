#nullable enable
using System;
using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.Services;

public interface ISubscriptionTracker {
	void AddSubscription(Guid subscriptionId, string? streamName, long endOfStream);
	void RemoveSubscription(Guid subscriptionId);
	void ProcessEvent(Guid subscriptionId, ResolvedEvent @event);
	void UpdateStreamPositions<T>(IReadIndex<T> readIndex);
}

public class SubscriptionTracker(SubscriptionMetric metric) : ISubscriptionTracker {
	public void AddSubscription(Guid subscriptionId, string? streamName, long endOfStream) =>
		metric.Add(subscriptionId, streamName, endOfStream);

	public void RemoveSubscription(Guid subscriptionId) => metric.Remove(subscriptionId);

	public void ProcessEvent(Guid subscriptionId, ResolvedEvent @event) => metric.ProcessEvent(subscriptionId, @event.OriginalStreamId,
		@event.OriginalPosition?.CommitPosition ?? 0L, @event.OriginalEventNumber);

	public void UpdateStreamPositions<T>(IReadIndex<T> readIndex) => metric.UpdateStreamPositions(readIndex);

	public class NoOp : ISubscriptionTracker {
		public void AddSubscription(Guid subscriptionId, string? streamName, long endOfStream) {
		}

		public void RemoveSubscription(Guid subscriptionId) {
		}

		public void UpdateStreamPositions<T>(IReadIndex<T> readIndex) {
		}

		public void ProcessEvent(Guid subscriptionId, ResolvedEvent @event) {
		}
	}
}
