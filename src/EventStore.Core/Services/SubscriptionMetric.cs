#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.Services;

public class SubscriptionMetric {
	private readonly ConcurrentDictionary<Guid, SubscriptionStats> _subscriptionStats;

	public SubscriptionMetric(Meter meter, string name) {
		_subscriptionStats = new();

		meter.CreateObservableUpDownCounter($"{name}-subscription-position", SubscriptionPosition);
		meter.CreateObservableUpDownCounter($"{name}-stream-position", StreamPosition);
		meter.CreateObservableUpDownCounter($"{name}-subscription-count", SubscriptionCount);
	}

	public void Add(Guid subscriptionId, string? streamName, long end) =>
		_subscriptionStats.TryAdd(subscriptionId, new(subscriptionId, streamName, end));

	public void Remove(Guid subscriptionId) => _subscriptionStats.TryRemove(subscriptionId, out _);

	public void UpdateLastIndexedPositions<T>(IReadIndex<T> readIndex) {
		foreach (var (_, stats) in _subscriptionStats) {
			if (stats.StreamName is null) {
				_subscriptionStats.TryUpdate(stats.SubscriptionId, stats with {
					End = readIndex.LastIndexedPosition
				}, stats);
			} else {
				var streamId = readIndex.GetStreamId(stats.StreamName);

				var eventPosition = readIndex.GetStreamLastEventNumber(streamId);

				_subscriptionStats.TryUpdate(stats.SubscriptionId, stats with {
					End = eventPosition
				}, stats);
			}
		}
	}

	public void UpdateSubscriptionPosition(Guid subscriptionId, string? streamName, long position) {
		foreach (var (_, stats) in _subscriptionStats) {
			if (stats.SubscriptionId != subscriptionId) {
				continue;
			}

			if (stats.StreamName != streamName) {
				continue;
			}

			_subscriptionStats.TryUpdate(stats.SubscriptionId, stats with {
				Current = position
			}, stats);
		}
	}

	private IEnumerable<Measurement<long>> SubscriptionPosition() {
		foreach (var (_, stats) in _subscriptionStats) {
			ReadOnlySpan<KeyValuePair<string, object?>> tags = [stats.StreamNameTag, stats.SubscriptionIdTag];
			yield return new Measurement<long>(stats.Current, tags);
		}
	}

	private IEnumerable<Measurement<long>> StreamPosition() {
		foreach (var (_, stats) in _subscriptionStats) {
			ReadOnlySpan<KeyValuePair<string, object?>> tags = [stats.StreamNameTag, stats.SubscriptionIdTag];
			yield return new Measurement<long>(stats.End, tags);
		}
	}

	private long SubscriptionCount() => _subscriptionStats.Count;

	private readonly record struct SubscriptionStats(
		Guid SubscriptionId,
		string? StreamName,
		long Current = 0,
		long End = 0) {
		private const string StreamNameTagKey = "stream-name";
		private const string SubscriptionIdTagKey = "subscription-id";

		public KeyValuePair<string, object?> StreamNameTag { get; } =
			new(StreamNameTagKey, StreamName ?? SystemStreams.AllStream);

		public KeyValuePair<string, object?> SubscriptionIdTag { get; } = new(SubscriptionIdTagKey, SubscriptionId);
	}
}
