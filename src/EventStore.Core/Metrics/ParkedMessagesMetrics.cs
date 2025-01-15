using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace EventStore.Core.Metrics;

public class ParkedMessagesMetrics {
	private readonly CounterSubMetric _parkedByClient;
	private readonly CounterSubMetric _parkedMaxRetries;
	private readonly CounterSubMetric _parkedReplays;

	public ParkedMessagesMetrics(Meter meter, string name) {
		var counterMetric = new CounterMetric(meter, name);

		_parkedByClient = new CounterSubMetric(counterMetric, new [] {
			new KeyValuePair<string, object>("op", "park-message"),
			new KeyValuePair<string, object>("reason", "client-nak")
		});

		_parkedMaxRetries = new CounterSubMetric(counterMetric, new [] {
			new KeyValuePair<string, object>("op", "park-message"),
			new KeyValuePair<string, object>("reason", "max-retries")
		});

		_parkedReplays = new CounterSubMetric(counterMetric, new [] {
			new KeyValuePair<string, object>("op", "replay-parked-messages")
		});
	}

	public void OnParkedMessageByClient() => _parkedByClient.Add(1);
	public void OnParkedMessageDueToMaxRetries() => _parkedMaxRetries.Add(1);
	public void OnParkedMessagesReplayed() => _parkedReplays.Add(1);
}
