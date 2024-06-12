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

	public void Add(Guid subscriptionId, string? streamName, long endOfStream) =>
		_subscriptionStats.TryAdd(subscriptionId, new(subscriptionId, streamName, EndOfStream: endOfStream));

	public void Remove(Guid subscriptionId) => _subscriptionStats.TryRemove(subscriptionId, out _);

	public void UpdateStreamPositions<T>(IReadIndex<T> readIndex) {
		foreach (var (_, stats) in _subscriptionStats) {
			if (stats.StreamName is null) {
				_subscriptionStats.TryUpdate(stats.SubscriptionId, stats with {
					EndOfStream = readIndex.LastIndexedPosition
				}, stats);
			} else {
				var streamId = readIndex.GetStreamId(stats.StreamName);

				var eventPosition = readIndex.GetStreamLastEventNumber(streamId);

				_subscriptionStats.TryUpdate(stats.SubscriptionId, stats with {
					EndOfStream = eventPosition
				}, stats);
			}
		}
	}

	public void ProcessEvent(Guid subscriptionId, string? streamName, long commitPosition, long eventPosition) {
		foreach (var (_, stats) in _subscriptionStats) {
			if (stats.SubscriptionId != subscriptionId) {
				continue;
			}
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

	private long SubscriptionCount() => _subscriptionStats.Count;

	private readonly record struct SubscriptionStats(
		Guid SubscriptionId,
		string? StreamName,
		long SubscriptionPosition = 0L,
		long EndOfStream = 0L) {
		private const string StreamNameTagKey = "stream-name";
		private const string SubscriptionIdTagKey = "subscription-id";

		public KeyValuePair<string, object?> StreamNameTag { get; } =
			new(StreamNameTagKey, StreamName ?? SystemStreams.AllStream);

		public KeyValuePair<string, object?> SubscriptionIdTag { get; } = new(SubscriptionIdTagKey, SubscriptionId);
	}
}
