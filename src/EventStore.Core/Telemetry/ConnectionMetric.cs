using System.Diagnostics.Metrics;
using System.Diagnostics.Tracing;

namespace EventStore.Core.Telemetry;

public class ConnectionMetric : EventListener {
	private readonly UpDownCounter<long> _connectionsMetric;

	public ConnectionMetric(Meter meter, string name) {
		_connectionsMetric = meter.CreateUpDownCounter<long>(name);
	}

	protected override void OnEventSourceCreated(EventSource eventSource) {
		if (eventSource.Name is not "Microsoft-AspNetCore-Server-Kestrel")
			return;

		EnableEvents(eventSource, EventLevel.Verbose);
	}

	protected override void OnEventWritten(EventWrittenEventArgs eventData) {
		if (_connectionsMetric == null)
			return;

		switch (eventData.EventName) {
			case "ConnectionStart": {
				_connectionsMetric.Add(1);
				break;
			}
			case "ConnectionStop": {
				_connectionsMetric.Add(-1);
				break;
			}
		}
	}
}
