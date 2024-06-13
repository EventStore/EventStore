#nullable enable
using System;
using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.Services;

public interface ISubscriptionTracker {
	void AddSubscription(Guid subscriptionId, string? streamName, long end);
	void RemoveSubscription(Guid subscriptionId);
	void UpdateSubscriptionPosition(Guid subscriptionId, string? streamName, long position);
	void UpdateLastIndexedPositions<T>(IReadIndex<T> readIndex);
}

public class SubscriptionTracker(SubscriptionMetric metric) : ISubscriptionTracker {
	public void AddSubscription(Guid subscriptionId, string? streamName, long end) =>
		metric.Add(subscriptionId, streamName, end);

	public void RemoveSubscription(Guid subscriptionId) => metric.Remove(subscriptionId);

	public void UpdateSubscriptionPosition(Guid subscriptionId, string? streamName, long position) =>
		metric.UpdateSubscriptionPosition(subscriptionId, streamName, position);

	public void UpdateLastIndexedPositions<T>(IReadIndex<T> readIndex) => metric.UpdateLastIndexedPositions(readIndex);

	public class NoOp : ISubscriptionTracker {
		public void AddSubscription(Guid subscriptionId, string? streamName, long end) {
		}

		public void RemoveSubscription(Guid subscriptionId) {
		}

		public void UpdateLastIndexedPositions<T>(IReadIndex<T> readIndex) {
		}

		public void UpdateSubscriptionPosition(Guid subscriptionId, string? streamName, long position) {
		}
	}
}
