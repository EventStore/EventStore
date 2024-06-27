using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using EventStore.Core.Messages;

namespace EventStore.Core.Metrics;

public class PersistentSubscriptionInFlightMessagesMetric {
	private readonly ObservableCounterMetricMulti<long> _statsMetric;

	public PersistentSubscriptionInFlightMessagesMetric(Meter meter, string name) {
		_statsMetric = new ObservableCounterMetricMulti<long>(meter, upDown: true, name);
	}

	public void Register(Func<IReadOnlyList<MonitoringMessage.PersistentSubscriptionInfo>> getCurrentStatsList) {
		_statsMetric.Register(GetMeasurements);

		IEnumerable<Measurement<long>> GetMeasurements() {
			var currentStatsList = getCurrentStatsList();
			foreach (var statistics in currentStatsList) {
				yield return new(statistics.TotalInFlightMessages, [
					new("event_stream_id", statistics.EventSource),
					new("group_name", statistics.GroupName)
				]);
			}
		}
	}
}
