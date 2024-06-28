using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using EventStore.Core.Messages;

namespace EventStore.Core.Metrics;

public class PersistentSubscriptionItemsProcessedMetric {
	private readonly ObservableCounterMetricMulti<long> _statsMetric;

	public PersistentSubscriptionItemsProcessedMetric(Meter meter, string name) {
		_statsMetric = new ObservableCounterMetricMulti<long>(meter, upDown: false, name);
	}

	public void Register(Func<IReadOnlyList<MonitoringMessage.PersistentSubscriptionInfo>> getCurrentStatsList) {
		_statsMetric.Register(GetMeasurements);

		IEnumerable<Measurement<long>> GetMeasurements() {
			var currentStatsList = getCurrentStatsList();
			foreach (var statistics in currentStatsList) {
				yield return new(statistics.TotalItems, [
					new("event_stream_id", statistics.EventSource),
					new("group_name", statistics.GroupName)
				]);
			}
		}
	}
}
