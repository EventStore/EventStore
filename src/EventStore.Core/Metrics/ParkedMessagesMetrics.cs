using System.Collections.Generic;

namespace EventStore.Core.Metrics;

public class ParkedMessagesMetrics {
	private readonly CounterSubMetric _parkedByClient;
	private readonly CounterSubMetric _parkedMaxRetries;
	private readonly CounterSubMetric _parkedReplays;

	public ParkedMessagesMetrics(
		CounterMetric messagesParked,
		CounterMetric replaysRequested) {

		_parkedByClient = new CounterSubMetric(messagesParked, [
			new KeyValuePair<string, object>("reason", "client-nak")
		]);

		_parkedMaxRetries = new CounterSubMetric(messagesParked, [
			new KeyValuePair<string, object>("reason", "max-retries")
		]);

		_parkedReplays = new CounterSubMetric(replaysRequested, [
		]);
	}

	public void OnParkedMessageByClient() => _parkedByClient.Add(1);
	public void OnParkedMessageDueToMaxRetries() => _parkedMaxRetries.Add(1);
	public void OnParkedMessagesReplayed() => _parkedReplays.Add(1);
}
