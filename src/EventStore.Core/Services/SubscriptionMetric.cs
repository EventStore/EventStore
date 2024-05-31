#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using EventStore.Core.Messages;

namespace EventStore.Core.Services;

public class SubscriptionMetric {
	private readonly ConcurrentDictionary<Guid, MonitoringMessage.SubscriptionStats> _subscriptionStats;

	public SubscriptionMetric(Meter meter, string name) {
		_subscriptionStats = new();

		meter.CreateObservableUpDownCounter($"{name}-completed", Completed);
		meter.CreateObservableUpDownCounter($"{name}-subscription-position", SubscriptionPosition);
		meter.CreateObservableUpDownCounter($"{name}-stream-position", StreamPosition);
	}

	public void Add(Guid subscriptionId, string? streamName, long endOfStream) =>
		_subscriptionStats.TryAdd(subscriptionId, new(subscriptionId, streamName, EndOfStream: endOfStream));

	public void Remove(Guid subscriptionId) => _subscriptionStats.TryRemove(subscriptionId, out _);

	public void RecordEvent(string? streamName, long commitPosition, long eventPosition) {
		foreach (var (_, stats) in _subscriptionStats) {
			if (stats.StreamName == streamName) {
				_subscriptionStats.TryUpdate(stats.SubscriptionId, stats with {
					EndOfStream = eventPosition
				}, stats);
			} else if (stats.StreamName is null) {
				_subscriptionStats.TryUpdate(stats.SubscriptionId, stats with {
					EndOfStream = commitPosition
				}, stats);
			}
		}
	}

	public void ProcessEvent(string? streamName, long commitPosition, long eventPosition) {
		foreach (var (_, stats) in _subscriptionStats) {
			if (stats.StreamName == streamName) {
				_subscriptionStats.TryUpdate(stats.SubscriptionId, stats with {
					SubscriptionPosition = eventPosition
				}, stats);
			} else if (stats.StreamName is null) {
				_subscriptionStats.TryUpdate(stats.SubscriptionId, stats with {
					SubscriptionPosition = commitPosition
				}, stats);
			}
		}
	}

	private IEnumerable<Measurement<double>> Completed() {
		foreach (var (_, stats) in _subscriptionStats) {
			ReadOnlySpan<KeyValuePair<string, object?>> tags = [stats.StreamNameTag, stats.SubscriptionIdTag];
			yield return new Measurement<double>(stats.PercentageComplete, tags);
		}
	}

	private IEnumerable<Measurement<long>> SubscriptionPosition() {
		foreach (var (_, stats) in _subscriptionStats) {
			ReadOnlySpan<KeyValuePair<string, object?>> tags = [stats.StreamNameTag, stats.SubscriptionIdTag];
			yield return new Measurement<long>(stats.SubscriptionPosition, tags);
		}
	}

	private IEnumerable<Measurement<long>> StreamPosition() {
		foreach (var (_, stats) in _subscriptionStats) {
			ReadOnlySpan<KeyValuePair<string, object?>> tags = [stats.StreamNameTag, stats.SubscriptionIdTag];
			yield return new Measurement<long>(stats.EndOfStream, tags);
		}
	}
}
