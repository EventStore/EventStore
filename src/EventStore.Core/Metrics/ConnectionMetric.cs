// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Diagnostics.Metrics;
using System.Diagnostics.Tracing;

namespace EventStore.Core.Metrics;

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
