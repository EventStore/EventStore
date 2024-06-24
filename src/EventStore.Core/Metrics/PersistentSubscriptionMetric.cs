using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using EventStore.Common.Utils;
using EventStore.Core.Messages;

namespace EventStore.Core.Metrics;

public class PersistentSubscriptionMetric {
	private readonly ObservableUpDownMetric<long> _statsMetric;

	public PersistentSubscriptionMetric(Meter meter, string name) {
		_statsMetric = new ObservableUpDownMetric<long>(meter, name);
	}

	public void Register(List<MonitoringMessage.PersistentSubscriptionInfo> statsList) {
		foreach (var stat in statsList) {
			var subscriptionName = $"{stat.EventSource}/{stat.GroupName}";

			Register(_statsMetric, subscriptionName, "subscription-total-items-processed", () => stat.TotalItems);
			Register(_statsMetric, subscriptionName, "subscription-connection-count",
				() => stat.Connections.Count);
			Register(_statsMetric, subscriptionName, "subscription-total-in-flight-messages",
				() => stat.TotalInFlightMessages);
			Register(_statsMetric, subscriptionName, "subscription-total-number-of-parked-messages",
				() => stat.ParkedMessageCount);
			Register(_statsMetric, subscriptionName, "subscription-oldest-parked-message", () => stat.OldestParkedMessage);

			if (stat.EventSource == "$all") {
				var (checkpointedCommitPosition, _) = EventPositionParser.ParseCommitPreparePosition(stat.LastCheckpointedEventPosition);
				var (eventCommitPosition, _) =
					EventPositionParser.ParseCommitPreparePosition(stat.LastKnownEventPosition);

				Register(_statsMetric, subscriptionName, "subscription-last-checkpointed-event-commit-position",
					() => checkpointedCommitPosition);

				Register(_statsMetric, subscriptionName, "subscription-last-known-event-commit-position",
					() => eventCommitPosition);
			} else {
				Register(_statsMetric, subscriptionName, "subscription-last-processed-event-number",
					() => long.TryParse(stat.LastCheckpointedEventPosition, out var lastEventPos)
						? lastEventPos
						: 0);

				Register(_statsMetric, subscriptionName, "subscription-last-known-event-number",
					() => long.TryParse(stat.LastKnownEventPosition, out var lastKnownMsg)
						? lastKnownMsg
						: 0);
			}
		}
	}

	private static void Register(
		ObservableUpDownMetric<long> metric,
		string subscriptionName,
		string metricName,
		Func<long> measurementProvider) {
		var tags = new KeyValuePair<string, object>[] {
			new("subscription", subscriptionName),
			new("kind", metricName)
		};

		metric.Register(() => new(measurementProvider(), tags));
	}
}
